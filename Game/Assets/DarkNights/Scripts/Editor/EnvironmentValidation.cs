using System;
using System.IO;
using Runtime.AppStartup;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 环境初始化及日常构建的只读前置检查。目标已存在时在任何写入前拒绝初始化；
    /// 缺少启动资源或 Addressable 映射时明确失败，绝不以重新生成资产修复引用。
    /// </summary>
    public static class EnvironmentValidation
    {
        public const string StartupPath = "Assets/Addressables/Datas/AppStartup/AppStartupSettings.asset";
        public const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        private static readonly string[] OutputPaths =
        {
            StartupPath, "Assets/Addressables/GameResources/GameCore.prefab",
            "Assets/Network/NetworkManager.prefab", ScenePath, "Assets/DefaultPrefabObjects.asset"
        };
        private static readonly string[] AddressPaths =
        {
            "Assets/Addressables/Datas/GlobalSO/GlobalScriptableObjectDatabase.asset",
            "Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset",
            "Assets/Addressables/Datas/GlobalSO/ObjectSingletonDatabase.asset",
            "Assets/Addressables/Datas/GlobalSO/ObjectArchetypeDatabase.asset",
            StartupPath, "Assets/Addressables/GameResources/GameCore.prefab", "Assets/Network/NetworkManager.prefab"
        };

        public static void RequireEmptyOutputs()
        {
            foreach (string path in OutputPaths)
                if (File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path))
                    throw new InvalidOperationException("Initialization requires unused output paths. Preserve existing asset: " + path);
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the current scene before first-time initialization.");
        }

        [MenuItem("YY/Dark Nights/Validate Environment")]
        public static void Validate()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Environment validation requires an idle Editor.");
            foreach (string path in OutputPaths)
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                    throw new InvalidOperationException("Required environment asset is missing: " + path);
            if (AssetDatabase.LoadAssetAtPath<AppStartupSettings>(StartupPath) == null)
                throw new InvalidOperationException("Invalid AppStartup settings type.");
            foreach (string path in AddressPaths)
                RequireAddress(path, Path.GetFileNameWithoutExtension(path));
        }

        public static void RequireAddress(string path, string address)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Addressable settings are missing.");
            string guid = AssetDatabase.AssetPathToGUID(path);
            var entry = string.IsNullOrEmpty(guid) ? null : settings.FindAssetEntry(guid);
            if (entry == null || entry.address != address || AssetDatabase.LoadMainAssetAtPath(path) == null)
                throw new InvalidOperationException("Missing or incorrect Addressable mapping: " + address + " -> " + path);
            int matches = 0;
            foreach (var group in settings.groups)
                if (group != null)
                    foreach (var candidate in group.entries)
                        if (candidate.address == address) matches++;
            if (matches != 1) throw new InvalidOperationException("Address must be unique: " + address);
        }
    }
}
