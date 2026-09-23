using System;
using System.Linq;
using DarkNights.Entry;
using DarkNights.Runtime.Config;
using Runtime.AppStartup;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 显式注册首批已导入的规则 JSON 和启动模块，保持素材文件及既有场景不变。
    /// 重复运行只核对已有映射；冲突时失败，不移动人工分组或覆盖已有地址。
    /// </summary>
    public static class GameContentSetup
    {
        public const string ConfigRoot = "Assets/DarkNights/Res/Config/";

        public static void Register()
        {
            EnvironmentValidation.Validate();
            ValidateJson();
            CheckEntry("balance.json", GameCatalogLoader.BalanceAddress, false);
            CheckEntry("pinewatch.json", GameCatalogLoader.LevelAddress, false);
            CheckEntry("balance.json", GameCatalogLoader.BalanceAddress, true);
            CheckEntry("pinewatch.json", GameCatalogLoader.LevelAddress, true);
            var startup = AssetDatabase.LoadAssetAtPath<AppStartupSettings>(EnvironmentValidation.StartupPath);
            startup.EnsureDiscoveredModules();
            // OnValidate 可能已在内存中补入模块，仍需显式持久化；只保存本批资源。
            EditorUtility.SetDirty(startup);
            AssetDatabase.SaveAssetIfDirty(startup);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            AssetDatabase.SaveAssetIfDirty(settings);
            foreach (var group in settings.groups)
                if (group != null && group.entries.Any(entry =>
                    entry.address == GameCatalogLoader.BalanceAddress || entry.address == GameCatalogLoader.LevelAddress))
                    AssetDatabase.SaveAssetIfDirty(group);
            Validate();
            Debug.Log("DARK_NIGHTS_CONFIG_REGISTERED files=2");
        }

        public static void Validate()
        {
            EnvironmentValidation.Validate();
            ValidateJson();
            EnvironmentValidation.RequireAddress(ConfigRoot + "balance.json", GameCatalogLoader.BalanceAddress);
            EnvironmentValidation.RequireAddress(ConfigRoot + "pinewatch.json", GameCatalogLoader.LevelAddress);
            var startup = AssetDatabase.LoadAssetAtPath<AppStartupSettings>(EnvironmentValidation.StartupPath);
            if (!startup.Modules.Any(entry => entry.Enabled && entry.Required && entry.Module is GameContentStartupModule))
                throw new InvalidOperationException("Required game content startup module is not registered.");
            FormalObjectContentSetup.Validate();
            SessionNetworkSetup.Validate();
        }

        private static void ValidateJson()
        {
            var balance = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigRoot + "balance.json");
            var level = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigRoot + "pinewatch.json");
            if (balance == null || level == null) throw new InvalidOperationException("Import the two configuration assets first.");
            GameCatalogJson.Parse(balance.text, level.text);
        }

        private static void CheckEntry(string file, string address, bool create)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            string guid = AssetDatabase.AssetPathToGUID(ConfigRoot + file);
            var existing = settings.FindAssetEntry(guid);
            if (existing != null && existing.address != address)
                throw new InvalidOperationException("Preserve existing address for: " + file);
            foreach (var group in settings.groups)
                if (group != null && group.entries.Any(entry => entry.address == address && entry.guid != guid))
                    throw new InvalidOperationException("Address already belongs to another asset: " + address);
            if (create && existing == null)
            {
                if (settings.DefaultGroup == null) throw new InvalidOperationException("Default Addressable group is missing.");
                var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
                entry.address = address;
                EditorUtility.SetDirty(settings);
            }
        }
    }
}
