using System;
using System.IO;
using DarkNights.Runtime.Objects;
using DarkNights.View.Terrain;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkNights.Editor.Terrain
{
    /// <summary>地质功能样板资源的首版制作；只写指定空目录，创建原生事件Prefab、局部定义及确定性16像素标记。后续作者修改不由初始化覆盖，正式定义数据库保持原样。</summary>
    public static class GeologyLabAssets
    {
        public const string Root = "Assets/DarkNights/Res/Objects/GeologyEventLab";
        public const string PrefabPath = Root + "/GeologyEvent.prefab";

        public static Sprite Create()
        {
            if (Directory.Exists(Root) && Directory.GetFileSystemEntries(Root).Length != 0)
                throw new InvalidOperationException("地质事件输出目录非空，拒绝覆盖。");
            Folder(Root);
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[256];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(Root + "/CellMarker.png", texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(Root + "/CellMarker.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "/CellMarker.png");
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/CellMarker.png");
            var root = new GameObject("GeologyEvent");
            try
            {
                var instance = root.AddComponent<ObjectInstance>();
                var view = root.AddComponent<GeologyEventView>();
                view.Warning = Marker("LandingWarning", root.transform, sprite, new Color(1, .4f, .1f, .3f));
                view.Warning.transform.localScale = new Vector3(3, 1, 1);
                view.Falling = Marker("LooseRock", root.transform, sprite, new Color(.52f, .48f, .42f)).transform;
                view.Falling.localScale = new Vector3(3, 1, 1);
                SetReference(instance, "_view", view); view.ForceRefreshAllReferences();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PrefabPath), settings.DefaultGroup);
            entry.address = "dark_nights.lab.geology_event";
            AssetDatabase.SaveAssetIfDirty(settings); AssetDatabase.SaveAssetIfDirty(settings.DefaultGroup);
            CreateDefinition("SupportCollapse", new GeologyEventConfig());
            CreateDefinition("RegionRockfall", new GeologyEventConfig
            { EventId = "region", EnterRegion = true, SourceU = 72, LandingU = 72 });
            return sprite;
        }

        private static void CreateDefinition(string name, GeologyEventConfig config)
        {
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.Name = name; definition.NetType = NetworkType.Local;
            definition.EditorSetIdentity(Guid.NewGuid().ToString("N"), "lab.geology." + config.EventId, true);
            definition.BehaviourTypes.Add(typeof(GeologyEventBehaviour).FullName);
            definition.SharedConfigs.Add(config.Freeze());
            definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(PrefabPath));
            AssetDatabase.CreateAsset(definition, Root + "/" + name + ".asset");
        }

        internal static SpriteRenderer Marker(string name, Transform parent, Sprite sprite, Color color)
        {
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            var art = root.AddComponent<SpriteRenderer>(); art.sprite = sprite; art.color = color; art.sortingOrder = -20;
            return art;
        }

        internal static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException("缺少原生绑定字段：" + field);
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
