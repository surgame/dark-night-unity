using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using YY.Features.Players.View;

namespace DarkNights.Editor
{
    /// <summary>
    /// 显式安装手持功能首版资产：只向新目录创建挂点及投射物 Prefab，并定向补充既有绑定和配置。
    /// 已安装时拒绝覆盖；不随普通导入、构建执行，不重建现有 GUID 或角色动画。
    /// </summary>
    public static class HandheldContentSetup
    {
        public const string Art = "Assets/DarkNights/Res/Art/Custom/Handheld";
        public const string Root = "Assets/DarkNights/Res/Objects/Handheld";

        public static void Install()
        {
            if (EditorApplication.isPlaying || Directory.Exists(Root))
                throw new InvalidOperationException("Handheld installation requires Edit mode and an absent output directory.");
            ImportArt();
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var material = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DarkNights/Res/Objects/Worker/Worker.prefab")
                .GetComponentsInChildren<SpriteRenderer>(true).First().sharedMaterial;
            GameObject handheld = CreateHandheld(material);
            foreach (string name in new[] { "Worker", "Spearman", "Archer" }) BindActor(name, handheld);
            CreateBallistic(material);
            var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset");
            if (definition.SharedConfigs.OfType<HandheldConfig>().Any()) throw new InvalidOperationException("Handheld config already exists.");
            definition.SharedConfigs.Add(new HandheldConfig()); EditorUtility.SetDirty(definition);
            const string actionsPath = "Assets/DarkNights/Res/Input/Gameplay.inputactions";
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(actionsPath);
            var player = actions.FindActionMap("Player", true);
            if (player.FindAction("Item4") != null) throw new InvalidOperationException("Item4 already exists.");
            player.AddAction("Item4", InputActionType.Button, "<Keyboard>/4");
            File.WriteAllText(actionsPath, actions.ToJson());
            HandheldHudSetup.Install();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("DARK_NIGHTS_HANDHELD_INSTALLED actors=3 pooledEffect=1 sharedPrefab=1 icons=3 config=1 inputSlots=4");
        }

        private static void ImportArt()
        {
            foreach (string file in Directory.GetFiles(Art, "*.png"))
            {
                string path = file.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) throw new InvalidOperationException("Import PNG files before installing handheld content.");
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100; importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false; importer.sRGBTexture = true; importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                bool held = new[] { "Pistol.png", "Pickaxe.png", "Bomb.png", "Hand.png" }.Contains(Path.GetFileName(path));
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = held ? new Vector2(8f / 24, .5f) : new Vector2(.5f, .5f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings); importer.SaveAndReimport();
            }
        }

        private static GameObject CreateHandheld(Material material)
        {
            var root = new GameObject("Handheld", typeof(HandheldView));
            try
            {
                var view = root.GetComponent<HandheldView>();
                Transform pivot = Child(root.transform, "Hand Pivot");
                pivot.localPosition = new Vector3(.015f, .09f, 0);
                var item = Sprite(pivot, "Equipment", "Pistol", material, 8);
                var arm = Sprite(pivot, "Hand", "Hand", material, 9);
                var flash = Sprite(pivot, "Muzzle", "Muzzle", material, 10);
                flash.transform.localPosition = new Vector3(.09f, .025f, 0);
                NativePrefabBuilder.SetReference(view, "pivot", pivot);
                NativePrefabBuilder.SetReference(view, "item", item);
                NativePrefabBuilder.SetReference(view, "arm", arm);
                NativePrefabBuilder.SetReference(view, "flash", flash);
                var serialized = new SerializedObject(view);
                SetArray(serialized, "items", new UnityEngine.Object[] { ArtSprite("Pistol"), ArtSprite("Pickaxe"), ArtSprite("Bomb") });
                serialized.ApplyModifiedPropertiesWithoutUndo();
                flash.enabled = false;
                return PrefabUtility.SaveAsPrefabAsset(root, Root + "/Handheld.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BindActor(string name, GameObject handheld)
        {
            string path = "Assets/DarkNights/Res/Objects/" + name + "/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var actor = root.GetComponent<ActorView>();
                var serialized = new SerializedObject(actor);
                if (serialized.FindProperty("handheld").objectReferenceValue != null) throw new InvalidOperationException("Actor handheld already bound.");
                var facing = (Transform)serialized.FindProperty("facing").objectReferenceValue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(handheld, facing);
                var view = instance.GetComponent<HandheldView>();
                NativePrefabBuilder.SetReference(actor, "handheld", view);
                var data = new SerializedObject(view);
                var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                SetArray(data, "occupationalParts", renderers.Where(r => r.transform.parent.name == "FrontArm" || r.transform.parent.name == "BackArm").Cast<UnityEngine.Object>().ToArray());
                data.FindProperty("body").objectReferenceValue = renderers.Single(r => r.transform.parent.name == "Body");
                data.FindProperty("clothing").objectReferenceValue = serialized.FindProperty("clothing").objectReferenceValue;
                foreach (string pose in new[] { "idle", "walking" })
                {
                    SetArray(data, pose == "idle" ? "idle" : "walking", OriginalFrames("spr_worker_" + pose));
                    SetArray(data, pose == "idle" ? "shirtIdle" : "shirtWalking", OriginalFrames("spr_worker_shirt_" + pose));
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                view.Hide();
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static UnityEngine.Object[] OriginalFrames(string folder) => Directory.GetFiles(
            "Assets/DarkNights/Res/Art/Original/sprites/" + folder, "*.png")
            .OrderBy(p => int.Parse(Path.GetFileNameWithoutExtension(p).Substring(folder.Length + 1)))
            .Select(p => (UnityEngine.Object)AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\', '/'))).ToArray();

        private static void CreateBallistic(Material material)
        {
            var root = new GameObject("Ballistic");
            try
            {
                var instance = root.AddComponent<ObjectInstance>();
                var initializer = root.AddComponent<LocalObjectInstanceInitializer>();
                var owner = root.AddComponent<ObjectView>();
                NativePrefabBuilder.SetReference(instance, "_view", owner);
                NativePrefabBuilder.SetReference(initializer, "_objectInstance", instance);
                var effect = root.AddComponent<BallisticView>();
                NativePrefabBuilder.SetReference(effect, "surface", Sprite(root.transform, "Surface", "Bullet", material, 150));
                NativePrefabBuilder.SetReference(effect, "bullet", ArtSprite("Bullet"));
                NativePrefabBuilder.SetReference(effect, "bomb", ArtSprite("Bomb"));
                var data = new SerializedObject(effect);
                SetArray(data, "explosion", Enumerable.Range(0, 4).Select(i => (UnityEngine.Object)ArtSprite("Explosion" + i)).ToArray());
                data.ApplyModifiedPropertiesWithoutUndo();
                owner.EditorSetBindings(new[] { new ViewComponentBinding("ballistic", effect) }, false); owner.ForceRefreshAllReferences();
                string path = Root + "/Ballistic.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
                definition.Name = "主角池化投射物"; definition.NetType = NetworkType.Local;
                definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(path));
                AssetDatabase.CreateAsset(definition, Root + "/Ballistic.asset");
                definition.EditorSetIdentity(DefinitionIdentityAuthoring.ReadAssetGuid(definition), "effect.ballistic", false);
                EditorUtility.SetDirty(definition);
                var database = AssetDatabase.LoadAssetAtPath<ObjectDefinitionDatabase>("Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset");
                database.AddDefinition(definition); EditorUtility.SetDirty(database);
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                settings.CreateOrMoveEntry(definition.PrefabRef.AssetGUID, settings.DefaultGroup).address = "dark_nights.effect.ballistic";
                EditorUtility.SetDirty(settings); EditorUtility.SetDirty(settings.DefaultGroup);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        internal static Sprite ArtSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/" + name + ".png");
        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform; child.SetParent(parent, false); return child;
        }
        private static SpriteRenderer Sprite(Transform parent, string name, string art, Material material, int order)
        {
            var renderer = Child(parent, name).gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = ArtSprite(art); renderer.sharedMaterial = material; renderer.sortingOrder = order;
            return renderer;
        }
        internal static void SetArray(SerializedObject data, string name, UnityEngine.Object[] values)
        {
            var property = data.FindProperty(name); property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
