using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DarkNights.Runtime.Framework;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Definition;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkNights.Editor
{
    /// <summary>
    /// 显式首版素材安装入口：预检全部源字节和空输出，批量导入后创建十五类原生外观及定义映射。
    /// 普通构建不调用本工具；输入保留源哈希，安装完成后资源由人工维护，不允许重复覆盖。
    /// </summary>
    public static class NativeArtSetup
    {
        public const string InputPath = "Assets/DarkNights/Scripts/Editor/ArtInput.json";
        public const string OriginalRoot = "Assets/DarkNights/Res/Art/Original";
        private const string ObjectsRoot = "Assets/DarkNights/Res/Objects";

        [MenuItem("Dark Nights/Content/Install Initial Native Art")]
        public static void Install()
        {
            foreach (string name in new[] { "Worker", "House", "Trees" })
                CRefactorContentUpgrade.RequireGenerated(CRefactorContentUpgrade.PresentationType(name));
            string staging = Path.GetFullPath("../artifacts/art-staging/Original");
            JObject manifest = JObject.Parse(File.ReadAllText(staging + "/manifest.json"));
            JObject input = JObject.Parse(File.ReadAllText(InputPath));
            if (Hash(staging + "/manifest.json") != (string)input["manifestSha256"])
                throw new InvalidOperationException("Art manifest source changed.");
            RequireEmpty(OriginalRoot);
            RequireEmpty("Assets/DarkNights/Res/Shared/NativeArt");
            foreach (JObject spec in input["visuals"])
                if ((string)spec["name"] != "Worker") RequireEmpty(ObjectsRoot + "/" + (string)spec["name"]);
            foreach (JObject item in manifest["files"])
            {
                string path = staging + "/" + ((string)item["path"]).Substring("assets/".Length);
                if (new FileInfo(path).Length != (long)item["bytes"] || Hash(path) != (string)item["sha256"])
                    throw new InvalidOperationException("Frozen art mismatch: " + path);
            }
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string source in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
                {
                    string target = OriginalRoot + "/" + Path.GetRelativePath(staging, source);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(source, target, false);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CreateObjects(input);
            Validate();
            Debug.Log("DARK_NIGHTS_NATIVE_ART_INSTALLED files=551 visuals=15 clips=32 definitions=17");
        }

        private static void CreateObjects(JObject input)
        {
            EnsureFolder("Assets/DarkNights/Res/Shared/NativeArt");
            var material = new Material(Shader.Find("Unlit/Color"))
            {
                name = "Depleted", color = new Color(0.345098f, 0.3411765f, 0.2784314f, 1)
            };
            AssetDatabase.CreateAsset(material, "Assets/DarkNights/Res/Shared/NativeArt/Depleted.mat");
            var database = AssetDatabase.LoadAssetAtPath<ObjectDefinitionDatabase>("Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset");
            ObjectDefinition session = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(FormalObjectContentSetup.SessionDefinitionPath);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            foreach (JObject spec in input["visuals"])
            {
                string name = (string)spec["name"];
                string folder = ObjectsRoot + "/" + name;
                EnsureFolder(folder);
                GameObject prefab = NativePrefabBuilder.Create(spec, folder, material);
                if (name == "Worker")
                {
                    var worker = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(FormalObjectContentSetup.WorkerDefinitionPath);
                    worker.BehaviourTypes.Add(CRefactorContentUpgrade.PresentationType(name).FullName);
                    EditorUtility.SetDirty(worker);
                    continue;
                }
                string id = ContentId(name);
                var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
                definition.Name = name;
                definition.NetType = NetworkType.Local;
                definition.BehaviourTypes.Add(CRefactorContentUpgrade.PresentationType(name).FullName);
                definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)));
                AssetDatabase.CreateAsset(definition, folder + "/" + name + ".asset");
                string prefix = (string)spec["category"] == "actors" ? "unit." : (string)spec["category"] == "buildings" ? "building." : "worksite.";
                definition.EditorSetIdentity(DefinitionIdentityAuthoring.ReadAssetGuid(definition), prefix + id, false);
                EditorUtility.SetDirty(definition);
                database.AddDefinition(definition);
                definition.Type = DefinitionRuleIndex.TypeForKey(definition.Key);
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(definition.PrefabRef.AssetGUID, settings.DefaultGroup);
                entry.address = "dark_nights.object." + name.ToLowerInvariant();
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true, true);
            }
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        public static void Validate()
        {
            JObject input = JObject.Parse(File.ReadAllText(InputPath));
            JObject manifest = JObject.Parse(File.ReadAllText(OriginalRoot + "/manifest.json"));
            if (Hash(OriginalRoot + "/manifest.json") != (string)input["manifestSha256"])
                throw new InvalidOperationException("Frozen manifest changed.");
            foreach (JObject item in manifest["files"])
            {
                string path = OriginalRoot + "/" + ((string)item["path"]).Substring(7);
                if (Hash(path) != (string)item["sha256"]) throw new InvalidOperationException("Frozen art mismatch: " + path);
                if (!path.EndsWith(".png", StringComparison.Ordinal)) continue;
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed || importer.spritePixelsPerUnit != 100 ||
                    settings.spritePivot != new Vector2(0, 1) || importer.npotScale != TextureImporterNPOTScale.None)
                    throw new InvalidOperationException("Pixel importer contract failed: " + path);
            }
            foreach (JObject spec in input["visuals"])
            {
                string name = (string)spec["name"];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ObjectsRoot + "/" + name + "/" + name + ".prefab");
                if (prefab == null || prefab.GetComponent<DarkNights.View.NativeVisual>() == null)
                    throw new InvalidOperationException("Native visual missing: " + name);
                var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(ObjectsRoot + "/" + name + "/" + name + ".asset");
                CRefactorContentUpgrade.ValidateLocal(name, definition, prefab);
            }
        }

        public static string ContentId(string name) => name == "Trees" ? "wood" : name == "Farmland" ? "food" : name.ToLowerInvariant();

        private static string Hash(string path)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static void RequireEmpty(string path)
        {
            if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length != 0)
                throw new InvalidOperationException("Initial art output must be empty: " + path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
