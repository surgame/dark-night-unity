using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>显式菜单提交的采矿影响范围 Editor 批次；有界执行、逐项记账，不启动第二个 Unity 或覆盖历史报告。</summary>
    public sealed class MiningGridValidation : ICallbacks
    {
        private static MiningGridValidation active;
        private TestRunnerApi api;
        private string run, output;
        private double deadline;
        private readonly JArray results = new JArray();
        private int scheduled;
        public static string EvidenceRoot => Path.GetFullPath("../artifacts/mining-grid-20260930");

        [DarkNightsWorkbenchCommand("Dark Nights/Verify/Mining Grid Tests", "采集网格检查")]
        public static void Run() => Schedule(false);

        [DarkNightsWorkbenchCommand("Dark Nights/Verify/Mining Grid Focused Retry", "采集网格定向复测")]
        public static void Retry() => Schedule(true);

        private static void Schedule(bool focused)
        {
            if (active != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Editor 尚有活动任务。");
            Directory.CreateDirectory(EvidenceRoot);
            var batch = active = new MiningGridValidation();
            batch.output = Path.Combine(EvidenceRoot, "editor-" + DateTime.Now.ToString("HHmmss") + ".json");
            batch.api = ScriptableObject.CreateInstance<TestRunnerApi>(); batch.api.RegisterCallbacks(batch);
            batch.api.RetrieveTestList(TestMode.EditMode, root =>
            {
                var names = new List<string>();
                void Visit(ITestAdaptor test)
                {
                    if (!test.IsSuite && test.FullName.StartsWith("DarkNights.Tests.") &&
                        (test.FullName.Contains("MiningSelectorTests") || test.FullName.Contains("MiningInputTests") ||
                        test.FullName.Contains("PickaxeWithoutGridTarget") ||
                        !focused && (test.FullName.Contains("HeroControlTests") ||
                        test.FullName.Contains("HeroCombatTests") || test.FullName.Contains("TerrainShapeTests") ||
                        test.FullName.Contains("UnifiedTransactionTests")))) names.Add(test.FullName);
                    if (test.Children != null) foreach (var child in test.Children) Visit(child);
                }
                Visit(root); batch.scheduled = names.Count;
                if (names.Count == 0) throw new InvalidOperationException("未发现本批测试。");
                batch.deadline = EditorApplication.timeSinceStartup + 300;
                EditorApplication.update += batch.Watch;
                batch.run = batch.api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = names.ToArray() }));
            });
        }

        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.Test.IsSuite) return;
            results.Add(new JObject { ["name"] = result.Test.FullName, ["status"] = result.TestStatus.ToString(),
                ["message"] = result.Message, ["stack"] = result.StackTrace });
        }
        public void RunFinished(ITestResultAdaptor result) => Finish("completed");
        private void Watch()
        {
            if (EditorApplication.timeSinceStartup < deadline) return;
            TestRunnerApi.CancelTestRun(run); Finish("timeout");
        }
        private void Finish(string status)
        {
            if (active != this) return;
            EditorApplication.update -= Watch; api.UnregisterCallbacks(this); active = null;
            File.WriteAllText(output, new JObject { ["status"] = status, ["scheduled"] = scheduled, ["results"] = results }.ToString());
            Debug.Log("MINING_GRID_EDITOR " + status + " count=" + results.Count + " report=" + output);
        }
    }
}
