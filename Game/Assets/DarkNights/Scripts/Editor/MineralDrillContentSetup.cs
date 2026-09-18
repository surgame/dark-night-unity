using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Types;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YY.Features.Players.View;

namespace DarkNights.Editor
{
    /// <summary>
    /// 从已维护 Stone 原生表现复制出正式矿脉钻机 Worksite；钻机是可保存、可索引的 YYGC 对象，不接受整数占位。
    /// 只在指定空目录执行一次，源 Stone 资产、GUID 和普通工作点定义保持不变。
    /// </summary>
    public static class MineralDrillContentSetup
    {
        public const string Root = "Assets/DarkNights/Res/Objects/MineralDrill";
        public const string PrefabPath = Root + "/MineralDrill.prefab";
        public const string DefinitionPath = Root + "/MineralDrill.asset";
        private const string SourcePrefabPath = "Assets/DarkNights/Res/Objects/Stone/Stone.prefab";
        private const string DatabasePath = "Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset";
        private const string Key = "worksite.mineral-drill";
        private const string Address = "dark_nights.object.mineral_drill";

        [MenuItem("Dark Nights/Content/Install Mineral Drill Object")]
        public static void Install()
        {
            if (!File.Exists(SourcePrefabPath)) throw new InvalidOperationException("Stone source Prefab is missing.");
            if (Directory.Exists(Root) && Directory.GetFileSystemEntries(Root).Length != 0)
                throw new InvalidOperationException("Mineral drill installation requires an empty directory.");
            EnsureFolder(Root);
            ObjectDefinitionDatabase database = Required<ObjectDefinitionDatabase>(DatabasePath);
            database.RebuildLookup();
            if (database.GetDefinitionByKey(Key) != null) throw new InvalidOperationException("Mineral drill definition already exists.");
            GameObject prefab = CreatePrefab();
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.Name = "矿脉钻机";
            definition.NetType = NetworkType.Local;
            definition.Type = ObjectType.Scenery_ResourceNode;
            definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(PrefabPath));
            AssetDatabase.CreateAsset(definition, DefinitionPath);
            definition.EditorSetIdentity(DefinitionIdentityAuthoring.ReadAssetGuid(definition), Key, false);
            definition.BehaviourTypes.Add(typeof(WorksitePresentationBehaviour).FullName);
            ObjectCapabilitySetup.ConfigureLocal(definition, "iron");
            database.AddDefinition(definition);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PrefabPath), settings.DefaultGroup);
            entry.address = Address;
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(database);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(settings.DefaultGroup);
            AssetDatabase.SaveAssets();
            Validate(database, prefab, definition);
            Debug.Log("DARK_NIGHTS_MINERAL_DRILL_INSTALLED prefab=1 definition=1");
        }

        public static void Validate()
        {
            ObjectDefinitionDatabase database = Required<ObjectDefinitionDatabase>(DatabasePath);
            Validate(database, Required<GameObject>(PrefabPath), Required<ObjectDefinition>(DefinitionPath));
        }

        private static GameObject CreatePrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SourcePrefabPath);
            try { root.name = "MineralDrill"; return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void Validate(ObjectDefinitionDatabase database, GameObject prefab, ObjectDefinition definition)
        {
            database.RebuildLookup();
            if (definition.Type != ObjectType.Scenery_ResourceNode || definition.Key != Key ||
                definition.PrefabRef == null || definition.PrefabRef.AssetGUID != AssetDatabase.AssetPathToGUID(PrefabPath) ||
                database.GetDefinitionByKey(Key) != definition || prefab.GetComponent<ObjectInstance>() == null ||
                prefab.GetComponent<LocalObjectInstanceInitializer>() == null || prefab.GetComponent<WorksiteView>() == null ||
                !definition.BehaviourTypes.Contains(typeof(WorksiteBehaviour).FullName) ||
                !definition.BehaviourTypes.Contains(typeof(WorksitePresentationBehaviour).FullName))
                throw new InvalidOperationException("Mineral drill definition or Prefab binding is invalid.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        private static T Required<T>(string path) where T : UnityEngine.Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            return value != null ? value : throw new InvalidOperationException("Required asset is missing: " + path);
        }
    }
}
