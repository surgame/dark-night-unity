using System;
using System.IO;
using System.Linq;
using DarkNights.Entry.MapModule;
using DarkNights.Runtime.Physics;
using DarkNights.View.MapModule;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor.MapModule
{
    /// <summary>
    /// M17有限原生制作批次；只在独立空目录建立首版地形、环境绳、Profile和研究Scene。
    /// 沿已保存入口复制Rig引用，保留当前未保存场景与作者源；不运行Play或替换正式地图。
    /// </summary>
    public static class M17StudySetup
    {
        public const string ScenePath = "Assets/DarkNights/Res/Scenes/Workbenches/MapModule/MapModuleRopeRiftStudy.unity";
        public const string Root = "Assets/DarkNights/Res/Terrain/MapModule/M17";

        public static object Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                AssetDatabase.IsValidFolder(Root) || AssetDatabase.LoadMainAssetAtPath(ScenePath) != null)
                throw new InvalidOperationException("M17初始化要求Editor空闲、目标目录及Scene尚不存在。");
            Scene active = SceneManager.GetActiveScene();
            MapModulePaths.EnsureFolder(Root);
            var profileSource = AssetDatabase.LoadAssetAtPath<PhysicsProfile>(PhysicsStudySetup.ProfilePath);
            if (profileSource == null) throw new InvalidOperationException("缺少已锁定的合作物理Profile。");
            var profile = UnityEngine.Object.Instantiate(profileSource);
            profile.name = "M17 environment rope";
            AssetDatabase.CreateAsset(profile, Root + "/M17Physics.asset");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MapModulePaths.Root + "/Whitebox.mat");
            if (material == null) throw new InvalidOperationException("缺少现有白模材质。");
            GameObject module = CreateTerrain(material);
            GameObject rope = CreateRope(profile);
            AddClimbAction();
            if (!AssetDatabase.CopyAsset(MapModulePaths.Scene, ScenePath))
                throw new InvalidOperationException("未能复制已保存的研究Rig。");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var study = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapModuleStudyAuthoring>(true)).Single();
                var entry = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapModuleEntry>(true)).Single();
                var sample = study.Cases.Single();
                foreach (var old in sample.Modules) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var terrain = (GameObject)PrefabUtility.InstantiatePrefab(module, sample.transform);
                terrain.transform.position = new Vector3(0, -5.76f, 0);
                var hanging = (GameObject)PrefabUtility.InstantiatePrefab(rope, terrain.transform);
                hanging.transform.localPosition = new Vector3(6.16f, 8.8f, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(hanging.transform);
                study.StudyId = "m17-rope-rift"; study.DisplayName = "M17 · 悬索裂谷";
                study.name = study.DisplayName; sample.CaseId = "rope-rift"; sample.DisplayName = "可攀爬悬索裂谷";
                sample.name = sample.DisplayName; sample.Modules = new[] { terrain.GetComponent<MapModuleAuthoring>() };
                sample.Connections = Array.Empty<MapModuleConnection>(); sample.Bake = null;
                sample.Spawn.position = new Vector3(1.28f, 0, 0);
                entry.HeroPlacement.transform.position = sample.Spawn.position;
                entry.DefaultCaseId = sample.CaseId; entry.PhysicsProfile = profile;
                entry.PhysicsProps = Array.Empty<PhysicsPropPlacement>();
                entry.HangingRopes = new[] { hanging.GetComponent<HangingRopePlacement>() };
                entry.Stage.SceneCamera.transform.position = new Vector3(6.32f, -.5f, -10);
                entry.Stage.SceneCamera.orthographicSize = 5.2f;
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("M17作者Scene未保存。");
                var bake = MapModuleCompiler.Prepare(study, sample);
                var definition = entry.FreezePhysics();
                AssetDatabase.SaveAssets();
                return new { scene = ScenePath, module = AssetDatabase.GetAssetPath(module),
                    bake = bake.ContentHash, source = bake.SourceHash, physics = definition.Identity,
                    segments = definition.Props.Count, ropes = definition.HangingRopes.Count,
                    missingScripts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                        .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) };
            }
            finally
            {
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static GameObject CreateTerrain(Material material)
        {
            var root = new GameObject("M17 悬索裂谷");
            try
            {
                var author = root.AddComponent<MapModuleAuthoring>();
                author.ModuleId = "m17-rope-rift"; author.DisplayName = root.name;
                author.Footprint = new Vector2Int(4, 3);
                author.Solids = new[]
                {
                    Solid("banks-and-recovery", new[] { P(0,0), P(80,0), P(80,36), P(66,36),
                        P(66,10), P(56,10), P(30,36), P(0,36) }),
                    Solid("ceiling", new[] { P(0,56), P(80,56), P(80,60), P(0,60) }),
                    Solid("reward-shelf", new[] { P(68,44), P(80,44), P(80,47), P(68,47) })
                };
                author.Ports = new[]
                {
                    new MapModulePort { Id = "entry", From = new Vector2Int(0,36), To = new Vector2Int(0,56), Normal = Vector2Int.left },
                    new MapModulePort { Id = "exit", From = new Vector2Int(80,36), To = new Vector2Int(80,44), Normal = Vector2Int.right }
                };
                var mesh = MapModulePrefabFactory.BuildPreview(author);
                AssetDatabase.CreateAsset(mesh, Root + "/M17Terrain-preview.asset");
                var child = new GameObject("Contour preview"); child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                author.PreviewRenderer = child.AddComponent<MeshRenderer>();
                author.PreviewRenderer.sharedMaterial = material;
                return PrefabUtility.SaveAsPrefabAsset(root, Root + "/M17Terrain.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject CreateRope(PhysicsProfile profile)
        {
            var root = new GameObject("环境吊绳");
            try
            {
                var placement = root.AddComponent<HangingRopePlacement>();
                placement.SegmentLength = .48f;
                var line = root.AddComponent<LineRenderer>();
                line.sharedMaterial = profile.RopeViewPrefab.sharedMaterial;
                line.useWorldSpace = false; line.positionCount = 2;
                line.SetPosition(0, Vector3.zero); line.SetPosition(1, Vector3.down * 3.84f);
                line.startWidth = line.endWidth = .035f;
                line.startColor = line.endColor = new Color(.7f, .57f, .34f);
                line.sortingOrder = 12; placement.Preview = line;
                var display = UnityEngine.Object.Instantiate(root);
                try
                {
                    UnityEngine.Object.DestroyImmediate(display.GetComponent<HangingRopePlacement>());
                    profile.RopeViewPrefab = PrefabUtility.SaveAsPrefabAsset(display, Root + "/HangingRopeView.prefab")
                        .GetComponent<LineRenderer>();
                    EditorUtility.SetDirty(profile);
                }
                finally { UnityEngine.Object.DestroyImmediate(display); }
                var anchor = new GameObject("固定顶端"); anchor.transform.SetParent(root.transform, false);
                var sprite = anchor.AddComponent<SpriteRenderer>(); sprite.sprite = profile.PropViewPrefab.sprite;
                sprite.sharedMaterial = profile.PropViewPrefab.sharedMaterial; sprite.sortingOrder = 12;
                anchor.transform.localScale = new Vector3(.16f / sprite.sprite.bounds.size.x,
                    .16f / sprite.sprite.bounds.size.y, 1);
                return PrefabUtility.SaveAsPrefabAsset(root, Root + "/HangingRope.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void AddClimbAction()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(HeroInputAssetSetup.Path);
            var map = actions.FindActionMap("Player", true);
            if (map.FindAction("Climb", false) != null) return;
            var action = map.AddAction("Climb", InputActionType.Value, expectedControlLayout: "Axis");
            action.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/s").With("Positive", "<Keyboard>/w");
            action.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/downArrow").With("Positive", "<Keyboard>/upArrow");
            action.AddBinding("<Gamepad>/leftStick/y");
            File.WriteAllText(HeroInputAssetSetup.Path, actions.ToJson());
            AssetDatabase.ImportAsset(HeroInputAssetSetup.Path);
        }

        private static Vector2Int P(int x, int y) => new Vector2Int(x, y);
        private static MapModuleSolid Solid(string id, Vector2Int[] points) =>
            new MapModuleSolid { Id = id, Material = 2, Vertices = points };
    }
}
