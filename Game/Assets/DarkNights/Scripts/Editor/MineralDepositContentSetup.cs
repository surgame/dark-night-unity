using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Definition;
using GameCore.Objects.Types;
using GameCore.Objects.Runner;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YY.Features.Players.View;

namespace DarkNights.Editor
{
    /// <summary>
    /// 从已维护 Stone 原生表现复制出独立矿床 Prefab 和 ObjectDefinition；只在明确的空目录执行一次。
    /// 矿床使用独立 YYGC 状态与表现 Behaviour，源 Stone 资产、GUID 和普通工作点定义不会被改写。
    /// </summary>
    public static class MineralDepositContentSetup
    {
        public const string Root = "Assets/DarkNights/Res/Objects/MineralDeposit";
        public const string PrefabPath = Root + "/MineralDeposit.prefab";
        public const string DefinitionPath = Root + "/MineralDeposit.asset";
        private const string SourcePrefabPath = "Assets/DarkNights/Res/Objects/Stone/Stone.prefab";
        private const string DatabasePath = "Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset";

        public static void Install()
        {
            if (!File.Exists(SourcePrefabPath)) throw new InvalidOperationException("Stone source Prefab is missing.");
            if (Directory.Exists(Root) && Directory.GetFileSystemEntries(Root).Length != 0)
                throw new InvalidOperationException("Mineral deposit installation requires an empty directory.");
            EnsureFolder(Root);
            ObjectDefinitionDatabase database = Required<ObjectDefinitionDatabase>(DatabasePath);
            database.RebuildLookup();
            if (database.GetDefinitionByKey("scenery.mineral-deposit") != null)
                throw new InvalidOperationException("Mineral deposit definition already exists.");
            GameObject prefab = CreatePrefab();
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.Name = "矿床";
            definition.NetType = NetworkType.Local;
            definition.Type = ObjectType.Scenery_ResourceNode;
            definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(PrefabPath));
            AssetDatabase.CreateAsset(definition, DefinitionPath);
            definition.EditorSetIdentity(DefinitionIdentityAuthoring.ReadAssetGuid(definition), "scenery.mineral-deposit", false);
            definition.BehaviourTypes.Add(typeof(MineralDepositPresentationBehaviour).FullName);
            ObjectCapabilitySetup.ConfigureMineralDeposit(definition);
            database.AddDefinition(definition);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PrefabPath), settings.DefaultGroup);
            entry.address = "dark_nights.object.mineral_deposit";
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(database);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(settings.DefaultGroup);
            AssetDatabase.SaveAssets();
            Validate(database, prefab, definition);
            Debug.Log("DARK_NIGHTS_MINERAL_DEPOSIT_INSTALLED prefab=1 definition=1");
        }

        public static void Validate()
        {
            ObjectDefinitionDatabase database = Required<ObjectDefinitionDatabase>(DatabasePath);
            ObjectDefinition definition = Required<ObjectDefinition>(DefinitionPath);
            GameObject prefab = Required<GameObject>(PrefabPath);
            Validate(database, prefab, definition);
        }

        private static GameObject CreatePrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SourcePrefabPath);
            try
            {
                root.name = "MineralDeposit";
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void Validate(ObjectDefinitionDatabase database, GameObject prefab, ObjectDefinition definition)
        {
            database.RebuildLookup();
            if (definition.Type != ObjectType.Scenery_ResourceNode || definition.Key != "scenery.mineral-deposit" ||
                definition.PrefabRef == null || definition.PrefabRef.AssetGUID != AssetDatabase.AssetPathToGUID(PrefabPath) ||
                database.GetDefinitionByKey(definition.Key) != definition ||
                prefab.GetComponent<ObjectInstance>() == null || prefab.GetComponent<LocalObjectInstanceInitializer>() == null ||
                prefab.GetComponent<WorksiteView>() == null)
                throw new InvalidOperationException("Mineral deposit definition or Prefab binding is invalid.");
            if (!definition.BehaviourTypes.Contains(typeof(MineralDepositBehaviour).FullName) ||
                !definition.BehaviourTypes.Contains(typeof(MineralDepositPresentationBehaviour).FullName))
                throw new InvalidOperationException("Mineral deposit Behaviour list is incomplete.");
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
