using System;
using System.IO;
using System.Linq;
using DarkNights.Entry.MapModule;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Physics;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor
{
    /// <summary>
    /// 合作物理首版候选的原生制作入口；只向指定空目录写入，整Scene复制保留跨根显式引用。
    /// 作者地图及其Bake只读复用，不覆盖原场景、角色或冻结资源，生产不进入Play。
    /// </summary>
    public static class PhysicsStudySetup
    {
        public const string Root = "Assets/DarkNights/Res/Objects/CooperativePhysics";
        public const string ProfilePath = Root + "/CooperativePhysics.asset";
        public const string ScenePath = MapModuleEntry.PhysicsStudyScene;
        private const string SourceScene = "Assets/DarkNights/Res/Scenes/Workbenches/MapModule/MapModuleEntranceStudy.unity";

        public static string Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("物理候选装配需要空闲Editor。");
            if (AssetDatabase.LoadMainAssetAtPath(ScenePath) != null ||
                Directory.Exists(Root) && Directory.EnumerateFileSystemEntries(Root).Any())
                throw new InvalidOperationException("候选输出已存在；请打开已有资源，不覆盖作者修改。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("请先处理当前未保存场景。");
            var previous = SceneManager.GetActiveScene();
            var source = SceneManager.GetSceneByPath(SourceScene);
            bool opened = !source.IsValid() || !source.isLoaded;
            if (opened) source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            Scene candidate = default;
            try
            {
                MapModule.MapModulePaths.EnsureFolder(Root);
                MapModule.MapModulePaths.EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
                var sourceEntry = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapModuleEntry>(true)).Single();
                var bake = sourceEntry.Study.RequiredCase(sourceEntry.DefaultCaseId).Bake;
                if (bake == null) throw new InvalidOperationException("源研究场景需要已有固定Bake。");
                var profile = CreateProfile();
                InstallCapability();
                if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new InvalidOperationException("候选Scene副本未保存。");
                AssetDatabase.ImportAsset(ScenePath);
                candidate = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                var entry = candidate.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapModuleEntry>(true)).Single();
                entry.PhysicsProfile = profile;
                AssembleProps(entry);
                EditorUtility.SetDirty(entry);
                if (!EditorSceneManager.SaveScene(candidate)) throw new InvalidOperationException("候选物理绑定未保存。");
                AssetDatabase.SaveAssetIfDirty(profile);
                return entry.FreezePhysics().Identity;
            }
            finally
            {
                if (candidate.IsValid() && candidate.isLoaded && !candidate.isDirty) EditorSceneManager.CloseScene(candidate, true);
                if (opened && source.IsValid() && source.isLoaded && !source.isDirty) EditorSceneManager.CloseScene(source, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        public static void AssembleProps(MapModuleEntry entry)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                entry.gameObject.scene.path != ScenePath || entry.PhysicsProfile == null || entry.PhysicsProps.Length != 0)
                throw new InvalidOperationException("箱子仅装配到指定空候选Scene，拒绝重复或覆盖。");
            var sample = entry.Study.RequiredCase(entry.DefaultCaseId);
            var spawn = new Vector2(sample.Bake.SpawnX, sample.Bake.SpawnHeight);
            var positions = new[] { spawn + new Vector2(70, 12), spawn + new Vector2(190, 48) };
            entry.PhysicsProps = new PhysicsPropPlacement[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(entry.PhysicsProfile.PropViewPrefab.gameObject, entry.gameObject.scene);
                root.name = "箱子出生 " + (-i - 1);
                root.transform.position = new Vector3(positions[i].x / 100, positions[i].y / 100, 0);
                var placement = root.AddComponent<PhysicsPropPlacement>();
                placement.Id = -i - 1; placement.Visual = root.GetComponent<SpriteRenderer>();
                entry.PhysicsProps[i] = placement;
            }
        }

        private static PhysicsProfile CreateProfile()
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("缺少当前锁定2D管线着色器。");
            var material = new Material(shader) { name = "Cooperative physics view" };
            AssetDatabase.CreateAsset(material, Root + "/PhysicsView.mat");
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                bool border = x < 3 || y < 3 || x >= 29 || y >= 29;
                bool brace = Math.Abs(x - y) < 2 || Math.Abs(x + y - 31) < 2;
                texture.SetPixel(x, y, border ? new Color32(55, 46, 33, 255) :
                    brace ? new Color32(190, 157, 95, 255) : new Color32(122, 92, 56, 255));
            }
            texture.Apply();
            File.WriteAllBytes(Root + "/Box32.png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(Root + "/Box32.png", ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "/Box32.png");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = new Vector2(.5f, 0);
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Box32.png");
            if (sprite == null || Math.Abs(sprite.bounds.min.y) > .0001f)
                throw new InvalidOperationException("箱子实际精灵未应用脚底pivot，拒绝继续装配。");
            var boxRoot = new GameObject("Cooperative box");
            SpriteRenderer box;
            try
            {
                var renderer = boxRoot.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.sortingOrder = 20;
                box = PrefabUtility.SaveAsPrefabAsset(boxRoot, Root + "/Box.prefab").GetComponent<SpriteRenderer>();
            }
            finally { UnityEngine.Object.DestroyImmediate(boxRoot); }
            var ropeRoot = new GameObject("Cooperative rope");
            LineRenderer rope;
            try
            {
                var line = ropeRoot.AddComponent<LineRenderer>();
                line.sharedMaterial = material; line.useWorldSpace = true; line.positionCount = 2;
                line.startWidth = line.endWidth = .025f; line.sortingOrder = 21;
                line.startColor = line.endColor = new Color(.9f, .75f, .4f);
                rope = PrefabUtility.SaveAsPrefabAsset(ropeRoot, Root + "/Rope.prefab").GetComponent<LineRenderer>();
            }
            finally { UnityEngine.Object.DestroyImmediate(ropeRoot); }
            var profile = ScriptableObject.CreateInstance<PhysicsProfile>();
            profile.PropViewPrefab = box; profile.RopeViewPrefab = rope;
            AssetDatabase.CreateAsset(profile, ProfilePath);
            profile.Freeze();
            return profile;
        }

        private static void InstallCapability()
        {
            var session = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(DarkNightsWorkbenchCatalog.Session);
            if (session == null) throw new InvalidOperationException("缺少正式会话Definition。");
            string type = typeof(PhysicsSessionBehaviour).FullName;
            if (session.BehaviourTypes.Contains(type)) return;
            session.BehaviourTypes.Add(type); EditorUtility.SetDirty(session);
            AssetDatabase.SaveAssetIfDirty(session);
        }
    }
}
