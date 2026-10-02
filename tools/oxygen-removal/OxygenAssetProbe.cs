using System;
using System.IO;
using System.Linq;
using DarkNights.Editor;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Save;
using DarkNights.View;
using GameCore.Objects.Definition;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>氧气移除的原生资源验收探针；仅在正确候选且Editor空闲时校验注册、绑定及Prefab保存重开，报告不代替真实Play与联机。</summary>
public static class OxygenAssetProbe
{
    private const string StationGuid = "e4b73fb57e306934a8e0471288fb455b";
    private const string StationPrefabGuid = "f1ae1dbee38fe30428b6827ecab7eef1";
    public static string Run(string label)
    {
        if (SessionAuthority.ProtocolVersion != 23 || ObjectWorldSaveJson.FormatVersion != 16)
            throw new InvalidOperationException("Editor未加载氧气移除候选，拒绝使用旧基线验收。");
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode ||
            BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Editor通道正在使用。");
        if (string.IsNullOrEmpty(label) || label.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("验收标签非法。");
        string path = Path.GetFullPath("../artifacts/oxygen-removal-20261003/assets-" + label + ".json");
        if (File.Exists(path)) throw new IOException("验收报告已存在。");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var checks = new JArray();
        void Check(bool ok, string name)
        {
            checks.Add(new JObject { ["name"] = name, ["passed"] = ok });
            if (!ok) throw new InvalidOperationException(name);
        }
        try
        {
            GameContentSetup.Validate();
            Check(true, "正式配置、对象定义、组件绑定、启动和网络内容校验");
            var db = AssetDatabase.LoadAssetAtPath<ObjectDefinitionDatabase>(
                "Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset");
            Check(db.Definitions.All(d => d != null) && db.GetDefinitionByKey("expedition.oxygen") == null,
                "DefinitionDatabase无氧气及悬空对象");
            Check(Retired(StationGuid) && Retired(StationPrefabGuid), "退役Definition和Prefab不再导入");
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Check(settings.FindAssetEntry(StationGuid) == null &&
                settings.FindAssetEntry(StationPrefabGuid) == null, "Addressables无退役氧气条目");
            Panel("Assets/DarkNights/Res/UI/Expedition/Expedition.prefab", 11, Check);
            Panel("Assets/DarkNights/Res/Objects/ExpeditionShip/ShipHud.prefab", 16, Check);
            var report = new JObject { ["passed"] = true, ["utc"] = DateTime.UtcNow.ToString("O"),
                ["unity"] = Application.unityVersion, ["protocol"] = SessionAuthority.ProtocolVersion,
                ["saveVersion"] = ObjectWorldSaveJson.FormatVersion, ["checks"] = checks };
            File.WriteAllText(path, report.ToString());
        }
        catch (Exception error)
        {
            File.WriteAllText(path, new JObject { ["passed"] = false, ["checks"] = checks,
                ["error"] = error.ToString() }.ToString());
            throw;
        }
        return path;
    }

    private static void Panel(string path, int count, Action<bool, string> check)
    {
        string guid = AssetDatabase.AssetPathToGUID(path);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var panel = root.GetComponentsInChildren<ExpeditionPanel>(true).Single();
            check(panel.Actions.Length == count && panel.Commands.Length == count &&
                panel.Actions.All(a => a != null) && panel.Status != null, path + "绑定完整");
            check(panel.Commands.Distinct().Count() == count && !panel.Commands.Contains("relay"),
                path + "命令唯一且无中继");
            check(root.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), path + "无MissingScript");
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var panel = root.GetComponentsInChildren<ExpeditionPanel>(true).Single();
            check(panel.Actions.Length == count && panel.Commands.Length == count &&
                panel.Actions.All(a => a != null) && AssetDatabase.AssetPathToGUID(path) == guid,
                path + "保存重开及GUID保持");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static bool Retired(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        // Unity会保留本Editor会话内已删除资产的GUID；只检查实际存在的资产，不要求历史路径缓存立刻消失。
        return string.IsNullOrEmpty(path) || !File.Exists(path) &&
            AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets) == "" &&
            AssetDatabase.LoadMainAssetAtPath(path) == null;
    }
}
