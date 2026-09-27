using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>航程候选有限 Editor 批次；一次提交并写原子报告，超时保留已完成结果，不把未执行算通过。</summary>
public sealed class JourneyEditorBatch : ICallbacks
{
    private static JourneyEditorBatch active;
    private TestRunnerApi api;
    private string run, output, current = "";
    private double deadline;
    private int scheduled;
    private readonly JArray results = new JArray();
    public static string Run(string label, string filter = "Journey")
    {
        if (active != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Editor already occupied.");
        string output = Path.GetFullPath("../artifacts/space-planet-flow/editor-" + label + ".json");
        if (File.Exists(output)) throw new IOException("Run identity exists.");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var batch = active = new JourneyEditorBatch { output = output };
        batch.api = ScriptableObject.CreateInstance<TestRunnerApi>(); batch.api.RegisterCallbacks(batch);
        batch.api.RetrieveTestList(TestMode.EditMode, root =>
        {
            var names = new List<string>();
            void Visit(ITestAdaptor test)
            {
                if (!test.IsSuite && test.FullName.StartsWith("DarkNights.Tests.") && !test.FullName.Contains("PlayTests") &&
                    filter.Split('|').Any(term => term == "all" ||
                        (term == "baseline" ? !test.FullName.Contains("Journey") : test.FullName.Contains(term)))) names.Add(test.FullName);
                if (test.Children != null) foreach (var child in test.Children) Visit(child);
            }
            Visit(root); batch.scheduled = names.Count; batch.deadline = EditorApplication.timeSinceStartup + 600;
            batch.Write("submitted");
            batch.run = batch.api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = names.ToArray() }));
            EditorApplication.update += batch.Watch;
        });
        return "Submitted " + output;
    }
    public void RunStarted(ITestAdaptor tests) { }
    public void TestStarted(ITestAdaptor test) { if (!test.IsSuite) current = test.FullName; }
    public void TestFinished(ITestResultAdaptor result)
    {
        if (result.Test.IsSuite) return;
        results.Add(new JObject { ["name"] = result.Test.FullName, ["status"] = result.TestStatus.ToString(),
            ["duration"] = result.Duration, ["message"] = result.Message, ["stack"] = result.StackTrace });
        Write("running");
    }
    public void RunFinished(ITestResultAdaptor result) => Finish("completed");
    private void Watch()
    {
        if (EditorApplication.timeSinceStartup < deadline) return;
        TestRunnerApi.CancelTestRun(run); Finish("timeout");
    }
    private void Write(string status)
    {
        var report = new JObject { ["status"] = status, ["scheduled"] = scheduled, ["current"] = current,
            ["utc"] = DateTime.UtcNow.ToString("O"), ["results"] = results };
        string temp = output + ".tmp"; File.WriteAllText(temp, report.ToString());
        if (File.Exists(output)) File.Replace(temp, output, null); else File.Move(temp, output);
    }
    private void Finish(string status)
    {
        if (active != this) return;
        EditorApplication.update -= Watch; Write(status); api.UnregisterCallbacks(this); active = null;
    }
}
