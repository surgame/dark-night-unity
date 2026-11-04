using System;
using System.IO;
using System.Linq;
using DarkNights.Entry.MapModule;
using DarkNights.Entry.Terrain;
using DarkNights.View;
using DarkNights.View.MapModule;
using GameCore.Objects.Runner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor.MapModule
{
    /// <summary>
    /// 授权首片的单次原生资产初始化；入口与继续深入各占4×3U，原生Scene拥有实例布局和Rig。
    /// 仅在空目标目录生产，复用已确认主角及现有输入资源；旧白模场景只读，失败不清理已有成果。
    /// </summary>
    internal static class MapModuleStudySetup
    {
        private const string SourceScene = "Assets/DarkNights/Res/Scenes/Workbenches/Terrain/CaveWhitebox.unity";
        internal static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("请先停止运行，再创建示例地图。");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MapModulePaths.Scene) != null)
                throw new InvalidOperationException("示例地图已存在，请打开已有场景。");
            if (Directory.Exists(MapModulePaths.Root) && Directory.EnumerateFileSystemEntries(MapModulePaths.Root).Any())
                throw new InvalidOperationException("地图目录中已有文件，请先检查是否已创建过示例地图。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("先保存当前场景。");
            var framing = AssetDatabase.LoadAssetAtPath<View.CameraFraming.CameraFramingConfig>(
                "Assets/DarkNights/Res/Shared/Camera/ExplorationCamera.asset");
            if (framing == null) throw new InvalidOperationException("缺少共享地面镜头配置，请检查ExplorationCamera资产。");
            MapModulePaths.EnsureFolder(MapModulePaths.Root); MapModulePaths.EnsureFolder(MapModulePaths.Modules);
            MapModulePaths.EnsureFolder(Path.GetDirectoryName(MapModulePaths.Scene).Replace('\\', '/'));
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("项目缺少2D Sprite Unlit着色器。");
            var material = new Material(shader) { name = "MapModule whitebox" };
            AssetDatabase.CreateAsset(material, MapModulePaths.Root + "/Whitebox.mat");
            var first = MapModulePrefabFactory.Create("surface-entry", "地表缓入",
                new[] { P(0, 0), P(80, 0), P(80, 18), P(76, 18), P(72, 16), P(56, 16),
                    P(48, 20), P(40, 24), P(32, 28), P(24, 32), P(16, 36), P(0, 36) },
                new[] { P(32, 40), P(40, 36), P(48, 36), P(50, 37), P(58, 37), P(62, 35), P(80, 35),
                    P(80, 56), P(64, 56), P(56, 60), P(48, 60), P(40, 56), P(32, 52) },
                new[] { Port("exit", 80, 18, 35, 1) }, material);
            var second = MapModulePrefabFactory.Create("shallow-hall", "浅厅续入",
                new[] { P(0, 0), P(80, 0), P(80, 14), P(64, 14), P(56, 10), P(48, 10),
                    P(40, 14), P(24, 14), P(16, 18), P(0, 18) },
                new[] { P(0, 35), P(16, 35), P(24, 39), P(40, 39), P(48, 35), P(80, 35),
                    P(80, 56), P(64, 56), P(56, 60), P(40, 60), P(32, 56), P(0, 56) },
                new[] { Port("entry", 0, 18, 35, -1), Port("exit", 80, 14, 35, 1) }, material);
            var source = SceneManager.GetSceneByPath(SourceScene);
            bool opened = !source.IsValid() || !source.isLoaded;
            if (opened) source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            try
            {
                var reference = source.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WhiteboxEntry>(true)).Single();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var study = new GameObject("地图布局").AddComponent<MapModuleStudyAuthoring>();
                var sample = new GameObject("缓入浅厅 · 继续深入").AddComponent<MapModuleCaseAuthoring>();
                sample.transform.SetParent(study.transform, false); sample.CaseId = "gentle"; sample.DisplayName = sample.name;
                var a = (GameObject)PrefabUtility.InstantiatePrefab(first, sample.transform);
                var b = (GameObject)PrefabUtility.InstantiatePrefab(second, sample.transform);
                a.transform.position = new Vector3(0, -5.76f, 0); b.transform.position = new Vector3(12.8f, -5.76f, 0);
                sample.Modules = new[] { a.GetComponent<MapModuleAuthoring>(), b.GetComponent<MapModuleAuthoring>() };
                sample.Connections = new[] { new MapModuleConnection
                    { From = sample.Modules[0], FromPort = "exit", To = sample.Modules[1], ToPort = "entry" } };
                sample.Spawn = new GameObject("出生锚点").transform; sample.Spawn.SetParent(sample.transform, false);
                sample.Spawn.position = new Vector3(1.92f, 0, 0);
                var preview = new GameObject("Frozen case preview"); preview.transform.SetParent(sample.transform, false);
                sample.PreviewMesh = preview.AddComponent<MeshFilter>();
                sample.PreviewRenderer = preview.AddComponent<MeshRenderer>(); sample.PreviewRenderer.sharedMaterial = material;
                study.Cases = new[] { sample };
                CreateRig(reference, study, scene, framing);
                if (!EditorSceneManager.SaveScene(scene, MapModulePaths.Scene)) throw new InvalidOperationException("示例地图的场景保存失败。");
                MapModuleCompiler.Prepare(study, sample);
                AssetDatabase.SaveAssets();
            }
            finally { if (opened && source.IsValid() && !source.isDirty) EditorSceneManager.CloseScene(source, true); }
            EditorSceneManager.OpenScene(MapModulePaths.Scene, OpenSceneMode.Single);
        }
        private static int[] P(int x, int y) => new[] { x, y };
        private static MapModulePort Port(string id, int x, int bottom, int top, int direction) =>
            new MapModulePort { Id = id, From = new Vector2Int(x, bottom), To = new Vector2Int(x, top), Normal = new Vector2Int(direction, 0) };
        private static void CreateRig(WhiteboxEntry reference, MapModuleStudyAuthoring study, Scene scene,
            View.CameraFraming.CameraFramingConfig framing)
        {
            string heroPath = MapModulePaths.Root + "/HeroRig.prefab";
            if (AssetDatabase.LoadMainAssetAtPath(heroPath) != null) throw new InvalidOperationException("主角Prefab已存在。");
            var copy = UnityEngine.Object.Instantiate(reference.HeroPlacement.gameObject);
            GameObject heroAsset;
            try
            {
                UnityEngine.Object.DestroyImmediate(copy.GetComponent<ScenePlacement>());
                UnityEngine.Object.DestroyImmediate(copy.GetComponent<ObjectDefinitionLoader>());
                if (copy.GetComponentsInChildren<ObjectDefinitionLoader>(true).Length != 0)
                    throw new InvalidOperationException("主角外观Prefab中有多余的加载器，请移除 ObjectDefinitionLoader 后再保存。");
                heroAsset = PrefabUtility.SaveAsPrefabAsset(copy, heroPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            var heroRoot = (GameObject)PrefabUtility.InstantiatePrefab(heroAsset, scene);
            heroRoot.name = "主角出生实例"; heroRoot.transform.position = new Vector3(1.92f, 0, 0);
            var loader = heroRoot.AddComponent<ObjectDefinitionLoader>();
            loader.EditorConfigure(reference.HeroPlacement.Loader.ResolveDefinition());
            var hero = heroRoot.AddComponent<ScenePlacement>();
            hero.EditorConfigure(loader, heroRoot.GetComponent<ActorView>(), reference.HeroPlacement.InitialVariant,
                reference.HeroPlacement.InitialName);
            var owner = new GameObject("场景入口");
            var entry = owner.AddComponent<MapModuleEntry>(); entry.Study = study; entry.HeroPlacement = hero;
            var stage = owner.AddComponent<PinewatchStage>(); entry.Stage = stage;
            var camera = UnityEngine.Object.Instantiate(reference.ViewCamera.gameObject).GetComponent<Camera>();
            // 试验场通过Bootstrap运行，监听器由Bootstrap唯一提供，镜头复制不能带入第二个。
            foreach (var listener in camera.GetComponentsInChildren<AudioListener>(true))
                UnityEngine.Object.DestroyImmediate(listener);
            camera.name = "主相机"; camera.transform.position = new Vector3(3, .4f, -10); camera.orthographicSize = 2.2f;
            camera.backgroundColor = new Color(.16f, .23f, .25f); camera.clearFlags = CameraClearFlags.SolidColor;
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(HeroInputAssetSetup.Path);
            if (actions == null) throw new InvalidOperationException("缺少正式Gameplay输入动作资产。");
            foreach (string map in new[] { "Camp", "Player", "UI" }) actions.FindActionMap(map, true);
            var input = new GameObject("玩家输入").AddComponent<PlayerInput>();
            input.actions = actions; input.defaultActionMap = "Camp";
            input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            input.neverAutoSwitchControlSchemes = true;
            input.camera = camera;
            var entities = new GameObject("运行时对象").transform; entities.SetParent(owner.transform, false);
            var sky = new GameObject("天空").AddComponent<SpriteRenderer>(); sky.transform.SetParent(owner.transform, false);
            var serialized = new SerializedObject(stage);
            serialized.FindProperty("sceneCamera").objectReferenceValue = camera;
            serialized.FindProperty("inputPlayer").objectReferenceValue = input;
            serialized.FindProperty("entities").objectReferenceValue = entities;
            serialized.FindProperty("sky").objectReferenceValue = sky;
            serialized.FindProperty("backgrounds").arraySize = 0;
            serialized.FindProperty("environment").objectReferenceValue = null;
            serialized.FindProperty("cameraX").floatValue = 192;
            serialized.FindProperty("zoom").floatValue = 2.8f;
            serialized.FindProperty("cameraFraming").objectReferenceValue = framing;
            serialized.FindProperty("worldWidth").floatValue = 5120;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
