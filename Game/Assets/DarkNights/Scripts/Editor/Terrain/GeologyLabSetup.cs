using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkNights.Entry.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.View.Terrain;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor.Terrain
{
    /// <summary>场景功能开发工作台的首版装配；只在新目录创建原生岩格、事件、UI和明确引用。通过单Editor API保存，既有场景、Prefab、定义及构建列表不改，打开入口不自动运行。</summary>
    public static class GeologyLabSetup
    {
        [MenuItem("Dark Nights/Tools/创建场景功能开发样板")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("请等待编译、退出Play并保存场景。");
            string directory = Path.GetDirectoryName(GeologyLabEntry.ScenePath);
            if (File.Exists(GeologyLabEntry.ScenePath) || Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length != 0)
                throw new InvalidOperationException("功能场景目录非空，拒绝覆盖。");
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (pipeline == null || !pipeline.GetType().FullName.Contains("Universal"))
                throw new InvalidOperationException("功能场景沿用当前锁定URP；不修改项目管线。");
            Sprite sprite = GeologyLabAssets.Create(); GeologyLabAssets.Folder(directory.Replace('\\', '/'));
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("GameplayLab"); var lab = root.AddComponent<GeologyLabEntry>();
                lab.SessionDefinition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(FormalObjectContentSetup.SessionDefinitionPath);
                lab.WorkerDefinition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(FormalObjectContentSetup.WorkerDefinitionPath);
                lab.ShipDefinition = ObjectDefinitionDatabase.Instance.Definitions.Single(d => d.SharedConfigs.OfType<BuildingRuleConfig>().Any(c => c.RuleKey == "ship"));
                var session = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FormalObjectContentSetup.SessionPrefabPath), scene);
                lab.SessionInstance = session.GetComponent<ObjectInstance>(); session.name = "LocalSession";
                lab.ViewCamera = new GameObject("LabCamera").AddComponent<Camera>();
                lab.ViewCamera.orthographic = true; lab.ViewCamera.orthographicSize = 1.8f;
                lab.ViewCamera.transform.position = new Vector3(8.6f, .12f, -10);
                lab.ViewCamera.clearFlags = CameraClearFlags.SolidColor; lab.ViewCamera.backgroundColor = new Color(.035f, .045f, .06f);
                lab.ViewCamera.allowHDR = lab.ViewCamera.allowMSAA = lab.ViewCamera.allowDynamicResolution = false;
                var cameraData = lab.ViewCamera.gameObject.AddComponent(Type.GetType(
                    "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime", true));
                var cameraSettings = new SerializedObject(cameraData);
                cameraSettings.FindProperty("m_RenderPostProcessing").boolValue = false;
                cameraSettings.FindProperty("m_Antialiasing").intValue = 0; cameraSettings.ApplyModifiedPropertiesWithoutUndo();
                var pixel = lab.ViewCamera.gameObject.AddComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
                pixel.assetsPPU = 100; pixel.refResolutionX = 640; pixel.refResolutionY = 360;
                pixel.gridSnapping = UnityEngine.Rendering.Universal.PixelPerfectCamera.GridSnapping.PixelSnapping;
                pixel.cropFrame = UnityEngine.Rendering.Universal.PixelPerfectCamera.CropFrame.None;
                lab.InputPlayer = root.AddComponent<PlayerInput>();
                lab.InputPlayer.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/DarkNights/Res/Input/Gameplay.inputactions");
                lab.InputPlayer.defaultActionMap = "Player";
                lab.Status = GeologyLabUi.Create(root.transform);
                var blocks = new List<GeologyLabBlock>();
                Block(blocks, root.transform, sprite, "ProtectedFloor", 0, -42, 100, 1, 8, true);
                Block(blocks, root.transform, sprite, "SupportToMine", 50, -41, 1, 1, 2, false);
                for (int i = 0; i < 3; i++) foreach (int u in new[] { 52, 72 })
                {
                    Block(blocks, root.transform, sprite, "LooseRock", u + i, -36, 1, 1, 2, false);
                    Block(blocks, root.transform, sprite, "RubbleLanding", u + i, -41, 1, 1, 0, false);
                }
                // 一格瓦砾上方保留至少四格净空；基础跳跃可越过，地面永不被本事件破坏。
                lab.Blocks = blocks.ToArray();
                lab.EventSlots = new[] { Event(scene, "SupportCollapse", sprite), Event(scene, "RegionRockfall", sprite) };
                var region = GeologyLabAssets.Marker("RegionTrigger", root.transform, sprite, new Color(1, .45f, .12f, .15f));
                region.transform.position = new Vector3(11.12f, -.08f, 0); region.transform.localScale = new Vector3(4, 3, 1);
                EditorSceneManager.SaveScene(scene, GeologyLabEntry.ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
            GeologyCaveSceneSetup.Upgrade();
        }

        private static void Block(List<GeologyLabBlock> blocks, Transform parent, Sprite sprite, string name,
            int u, int v, int width, int height, byte material, bool protect)
        {
            var art = GeologyLabAssets.Marker(name, parent, sprite, protect ? new Color(.22f, .28f, .34f) :
                name == "SupportToMine" ? new Color(.75f, .64f, .3f) : new Color(.55f, .5f, .43f));
            art.transform.position = new Vector3((u + (width - 1) * .5f) * .16f, (632 + (v - (height - 1) * .5f) * 16) / 100, 0);
            art.transform.localScale = new Vector3(width, height, 1); art.enabled = material != 0;
            var block = art.gameObject.AddComponent<GeologyLabBlock>();
            block.U = u; block.V = v; block.Width = width; block.Height = height; block.Material = material; block.Protected = protect; block.Art = art;
            blocks.Add(block);
        }

        private static GeologyLabEventSlot Event(Scene scene, string name, Sprite sprite)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GeologyLabAssets.PrefabPath), scene);
            root.name = name;
            var slot = root.AddComponent<GeologyLabEventSlot>();
            slot.Definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(GeologyLabAssets.Root + "/" + name + ".asset");
            slot.Instance = root.GetComponent<ObjectInstance>(); slot.View = root.GetComponent<GeologyEventView>();
            var config = slot.Definition.SharedConfigs.OfType<GeologyEventConfig>().Single();
            slot.View.Warning.transform.position = new Vector3((config.LandingU + 1) * .16f, (632 + config.LandingV * 16) / 100, 0);
            slot.View.Falling.position = new Vector3((config.SourceU + 1) * .16f, (632 + config.SourceV * 16) / 100, 0);
            slot.View.Falling.gameObject.SetActive(false); return slot;
        }

        [MenuItem("Dark Nights/工作台/打开场景功能开发样板")]
        public static void Open() => TerrainWorkbenchScenes.Open(GeologyLabEntry.ScenePath);
    }
}
