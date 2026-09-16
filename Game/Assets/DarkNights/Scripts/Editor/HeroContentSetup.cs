using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using GameCore.Objects.Definition;
using GameCore.Objects.Types;
using GameCore.UI.UGUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DarkNights.Editor
{
    /// <summary>
    /// 显式安装本次主角切片：为现有定义追加控制能力，并在 Pinewatch 追加输入和三块单向平台。
    /// 既有对象、GUID 与布局不重建；新增人工维护资产只允许首次生成，完成后保存并重新打开检查引用。
    /// </summary>
    public static class HeroContentSetup
    {
        public const string Scene = "Assets/DarkNights/Res/Scenes/Pinewatch/Pinewatch.unity";
        [MenuItem("Dark Nights/Content/Install Hero Input Slice")]
        public static void Install()
        {
            if (File.Exists(HeroInputAssetSetup.Path) || Directory.Exists(HeroHudSetup.Root))
                throw new InvalidOperationException("Hero content exists; do not regenerate authored assets.");
            var actions = HeroInputAssetSetup.Create();
            var database = AssetDatabase.LoadAssetAtPath<ObjectDefinitionDatabase>("Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset");
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ObjectDefinition", new[] { "Assets/DarkNights/Res/Objects" }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition.Type != ObjectType.Unit) continue;
                Add(definition, typeof(AutomaticActorControlBehaviour));
                string rule = definition.SharedConfigs.OfType<ActorRuleConfig>().Single().RuleKey;
                if (rule == "worker" || rule == "spearman" || rule == "archer")
                {
                    Add(definition, typeof(HeroControlBehaviour)); Add(definition, typeof(HeroMotionBehaviour));
                    Add(definition, typeof(HeroInventoryBehaviour));
                }
                EditorUtility.SetDirty(definition); count++;
            }
            var archetype = AssetDatabase.LoadAssetAtPath<ObjectArchetype>("Assets/DarkNights/Res/Shared/Archetypes/Actor.asset");
            string capability = typeof(IAutomaticActorControl).AssemblyQualifiedName;
            if (!archetype.RequiredCapabilityInterfaces.Contains(capability)) archetype.RequiredCapabilityInterfaces.Add(capability);
            EditorUtility.SetDirty(archetype);
            HeroHudSetup.Create(database);
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var stage = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PinewatchStage>(true)).Single();
            var layout = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelLayoutAuthoring>(true)).Single();
            var input = new GameObject("Gameplay Input").AddComponent<PlayerInput>();
            input.actions = actions; input.defaultActionMap = "Camp";
            input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            input.neverAutoSwitchControlSchemes = true;
            NativePrefabBuilder.SetReference(stage, "inputPlayer", input);
            CreatePlatforms(layout);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            Debug.Log("DARK_NIGHTS_HERO_INSTALLED actorDefinitions=" + count + " platforms=3 hud=1 actions=1");
        }

        private static void Add(ObjectDefinition definition, Type type)
        {
            if (!definition.BehaviourTypes.Contains(type.FullName)) definition.BehaviourTypes.Add(type.FullName);
        }

        /// <summary>只补既有帮助文本的明确绑定；冻结首版文字和几何不变，当前键位由业务展示更新。</summary>
        public static void InstallControlHints()
        {
            const string path = "Assets/DarkNights/Res/UI/Help/Help.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<UGUIView>();
                var bindings = view.Bindings.ToDictionary(b => b.Key, b => b.Target);
                if (bindings.ContainsKey("ControlsGuide")) throw new InvalidOperationException("Control guide binding already exists.");
                var label = root.transform.Find("CenterContainer2/PanelContainer3/MarginContainer4/VBoxContainer5/Label15").GetComponent<Text>();
                if (label == null) throw new InvalidOperationException("Existing control guide Text is required.");
                bindings.Add("ControlsGuide", label);
                NativeUiBuilder.Bind(view, bindings);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("DARK_NIGHTS_HERO_CONTROL_HINTS_BOUND=1");
        }

        /// <summary>一次性修正首版平台的纵轴方向及图形顶面；只保存既有平台，不重新生成场景或 GUID。</summary>
        public static void CorrectPlatformHeights()
        {
            const string path = "Assets/DarkNights/Res/Objects/HeroPlatform/HeroPlatform.prefab";
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var layout = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelLayoutAuthoring>()).Single();
            var platforms = layout.GetComponentsInChildren<HeroPlatform>();
            float ground = layout.transform.InverseTransformPoint(layout.GroundPoint).y;
            if (platforms.Length != 3 || platforms.Any(p => p.Id < 1 || p.Id > 3 ||
                Mathf.Abs(p.transform.localPosition.y - (ground - p.Id * 12)) > 0.001f))
                throw new InvalidOperationException("Expected uncorrected first-version platforms; do not overwrite authored changes.");
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var art = prefab.GetComponentsInChildren<SpriteRenderer>().Single().transform;
                if (art.localPosition != Vector3.zero)
                    throw new InvalidOperationException("Platform art has authored changes.");
                art.localPosition = new Vector3(0, -1, 0);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            foreach (var platform in platforms)
            {
                var position = platform.transform.localPosition;
                platform.transform.localPosition = new Vector3(position.x, ground + platform.Id * 12, position.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(platform.transform);
            }
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            Debug.Log("DARK_NIGHTS_PLATFORM_HEIGHTS_CORRECTED=3");
        }

        private static void CreatePlatforms(LevelLayoutAuthoring layout)
        {
            const string folder = "Assets/DarkNights/Res/Objects/HeroPlatform";
            if (Directory.Exists(folder)) throw new InvalidOperationException("Platform output must be absent.");
            Directory.CreateDirectory(folder);
            var texture = new Texture2D(2, 2); texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); texture.Apply();
            File.WriteAllBytes(folder + "/Platform.png", texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(folder + "/Platform.png", ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(folder + "/Platform.png");
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 1; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            var root = new GameObject("HeroPlatform", typeof(HeroPlatform));
            GameObject prefab;
            try
            {
                var art = new GameObject("Platform Art", typeof(SpriteRenderer)); art.transform.SetParent(root.transform, false);
                art.transform.localPosition = new Vector3(0, -1, 0);
                art.transform.localScale = new Vector3(24, 1, 1);
                var renderer = art.GetComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(folder + "/Platform.png");
                renderer.color = new Color32(170, 146, 111, 255); renderer.sortingOrder = 8;
                prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/HeroPlatform.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            var group = new GameObject("One Way Platforms").transform; group.SetParent(layout.transform, false);
            float ground = layout.transform.InverseTransformPoint(layout.GroundPoint).y;
            var platforms = new HeroPlatform[3];
            for (int i = 0; i < platforms.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                instance.transform.localPosition = new Vector3(182 + i * 28, ground + 12 * (i + 1), 0);
                platforms[i] = instance.GetComponent<HeroPlatform>(); platforms[i].Id = i + 1;
                PrefabUtility.RecordPrefabInstancePropertyModifications(platforms[i]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            }
            var serialized = new SerializedObject(layout); var field = serialized.FindProperty("platforms"); field.arraySize = platforms.Length;
            for (int i = 0; i < platforms.Length; i++) field.GetArrayElementAtIndex(i).objectReferenceValue = platforms[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
