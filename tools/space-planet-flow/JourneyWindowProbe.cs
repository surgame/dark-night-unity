using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DarkNights.Editor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>真实 Editor 窗口跨域重载探针；只改可放弃草稿，记录正式作者内容并在结束后恢复窗口。</summary>
public static class JourneyWindowProbe
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public static string Run(string operation)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Editor is playing.");
        string root = Path.GetFullPath("../artifacts/space-planet-flow");
        Directory.CreateDirectory(root);
        var windows = Resources.FindObjectsOfTypeAll<ExpeditionFlowWindow>();
        ExpeditionFlowWindow window;
        if (operation == "prepare")
        {
            if (windows.Length != 0) throw new InvalidOperationException("An author window is already open.");
            if (!EditorApplication.ExecuteMenuItem("Dark Nights/配置/星球与航程")) throw new InvalidOperationException("Menu unavailable.");
            window = Resources.FindObjectsOfTypeAll<ExpeditionFlowWindow>().Single();
            window.CreateGUI();
        }
        else window = windows.Single();
        var draft = (ExpeditionFlowDraft)typeof(ExpeditionFlowWindow).GetField("draft", Flags).GetValue(window);
        var source = (UnityEngine.Object)typeof(ExpeditionFlowDraft).GetField("source", Flags).GetValue(draft);
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (operation == "prepare")
        {
            using (var serialized = new SerializedObject(draft))
            {
                serialized.FindProperty("Config.Planets").GetArrayElementAtIndex(0).FindPropertyRelative("DisplayName").stringValue = "验收草稿：域重载保留";
                serialized.ApplyModifiedProperties();
            }
            window.CreateGUI();
        }
        var result = new JObject { ["operation"] = operation, ["sourcePath"] = sourcePath,
            ["sourceGuid"] = AssetDatabase.AssetPathToGUID(sourcePath), ["author"] = File.ReadAllText(sourcePath),
            ["draft"] = JsonUtility.ToJson(draft.Config), ["baseline"] = (string)typeof(ExpeditionFlowDraft).GetField("baselineJson", Flags).GetValue(draft),
            ["draftBaseline"] = (string)typeof(ExpeditionFlowDraft).GetField("draftBaselineJson", Flags).GetValue(draft),
            ["dirty"] = window.hasUnsavedChanges, ["utc"] = DateTime.UtcNow.ToString("O") };
        if (operation != "prepare")
        {
            var before = JObject.Parse(File.ReadAllText(Path.Combine(root, "window-prepare.json")));
            string[] keys = { "sourcePath", "sourceGuid", "author", "draft", "baseline", "draftBaseline", "dirty" };
            result["retained"] = keys.All(k => JToken.DeepEquals(before[k], result[k]));
        }
        File.WriteAllText(Path.Combine(root, "window-" + operation + ".json"), result.ToString());
        if (operation == "finish") { window.DiscardChanges(); window.Close(); }
        return new JObject { ["operation"] = operation, ["sourcePath"] = sourcePath, ["dirty"] = result["dirty"], ["retained"] = result["retained"] }.ToString();
    }
}
