using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

/// <summary>只读采集当前 Editor 批次已有测试结果；用于定位被单个异步场景阻塞的验收。</summary>
public static class ExpeditionAudit
{
    public static bool TestsActive() => (bool)typeof(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi)
        .GetMethod("IsRunActive", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
    public static string TestProgress()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.Pipeline.Editor.Testing.PipelineTestRunner")).First(t => t != null);
        var collector = type.GetField("m_ActiveCollector", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        if (collector == null) return "No active collector";
        var results = JArray.FromObject(collector.GetType().GetProperty("Results").GetValue(collector));
        string path = Path.GetFullPath("../artifacts/expedition/full-editor-partial.json");
        File.WriteAllText(path, results.ToString());
        return new JObject { ["completed"] = results.Count, ["last"] = results.Last,
            ["failures"] = new JArray(results.Where(t => (string)t["Status"] != "Passed")), ["path"] = path }.ToString();
    }
}
