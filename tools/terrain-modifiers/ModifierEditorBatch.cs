using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>一次执行当前非跨域 Play 的 Editor 快验；整批最多五分钟，保留失败和未完成状态。</summary>
public sealed class ModifierEditorBatch : ICallbacks
{
    private static ModifierEditorBatch active;
    private TestRunnerApi api;
    private string run;
    private double deadline;
    private readonly JArray results = new JArray();
    private string current = "";
    private int scheduled;
    private static string output;
    private static string Output => output;
    public static string Run(string label = "r1", string filter = "terrain")
    {
        if (active != null) throw new InvalidOperationException("Batch is already running");
        output = Path.GetFullPath("../artifacts/terrain-modifiers/editor-" + label + ".json");
        if (File.Exists(output)) throw new InvalidOperationException("Report exists; use a new run label.");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var batch = active = new ModifierEditorBatch();
        batch.api = ScriptableObject.CreateInstance<TestRunnerApi>(); batch.api.RegisterCallbacks(batch);
        batch.api.RetrieveTestList(TestMode.EditMode, root =>
        {
            var names = new List<string>();
            var retry = filter.StartsWith("failed-") ? JObject.Parse(File.ReadAllText(Path.GetFullPath("../artifacts/terrain-modifiers/editor-" + filter.Substring(7) + ".json")))["results"]
                .Where(t => (string)t["Status"] != "Passed" && !((string)t["FullName"]).Contains("NativeButtonThemeTests")).Select(t => (string)t["FullName"]).ToHashSet() : null;
            void Visit(ITestAdaptor test)
            {
                if (!test.IsSuite && test.FullName.StartsWith("DarkNights.Tests.") &&
                    !test.FullName.Contains("PlayTests") &&
                    (retry != null ? retry.Contains(test.FullName) : filter == "all" || filter == "terrain" && (test.FullName.Contains("Terrain") || test.FullName.Contains("Background") || test.FullName.Contains("Cave")) || filter != "terrain" && test.FullName.Contains(filter))) names.Add(test.FullName);
                if (test.Children != null) foreach (var child in test.Children) Visit(child);
            }
            Visit(root); batch.scheduled = names.Count; batch.deadline = EditorApplication.timeSinceStartup + 300;
            batch.run = batch.api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = names.ToArray() }));
            EditorApplication.update += batch.Watch;
        });
        return "Current Editor batch submitted; bounded to 300 seconds; " + Output;
    }
    public void RunStarted(ITestAdaptor tests) { }
    public void TestStarted(ITestAdaptor test) { if (!test.IsSuite) current = test.FullName; }
    public void TestFinished(ITestResultAdaptor result)
    {
        if (result.Test.IsSuite) return;
        results.Add(new JObject { ["FullName"] = result.Test.FullName, ["Status"] = result.TestStatus.ToString(),
            ["Duration"] = result.Duration, ["Message"] = result.Message, ["StackTrace"] = result.StackTrace });
        File.WriteAllText(Output, new JObject { ["status"] = "running", ["scheduled"] = scheduled,
            ["current"] = current, ["results"] = results }.ToString());
    }
    public void RunFinished(ITestResultAdaptor result) => Finish("completed");
    private void Watch()
    {
        if (EditorApplication.timeSinceStartup < deadline) return;
        TestRunnerApi.CancelTestRun(run); Finish("bounded-timeout");
    }
    private void Finish(string status)
    {
        if (active != this) return;
        EditorApplication.update -= Watch;
        File.WriteAllText(Output, new JObject { ["status"] = status, ["scheduled"] = scheduled,
            ["current"] = current, ["results"] = results }.ToString());
        api.UnregisterCallbacks(this); active = null;
    }
}
