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
    public sealed class MiningToolValidation : ICallbacks
    {
        private static MiningToolValidation active;
        private TestRunnerApi api;
        private string run, output;
        private double deadline;
        private readonly JArray results = new JArray();
        private int scheduled;
        public static string EvidenceRoot => Path.GetFullPath("../artifacts/tool-definition-harvesting-20261002");

        [MenuItem("Dark Nights/Verify/Tool Definition Tests")]
        public static void Run() => Schedule(false);

        [MenuItem("Dark Nights/Verify/Tool Definition Focused Retry")]
        public static void Retry() => Schedule(true);

        private static void Schedule(bool focused)
        {
            if (active != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Editor 尚有活动任务。");
            Directory.CreateDirectory(EvidenceRoot);
            var batch = active = new MiningToolValidation();
            batch.output = Path.Combine(EvidenceRoot, "editor-" + DateTime.Now.ToString("HHmmss") + ".json");
            batch.api = ScriptableObject.CreateInstance<TestRunnerApi>(); batch.api.RegisterCallbacks(batch);
            batch.api.RetrieveTestList(TestMode.EditMode, root =>
            {
                var names = new List<string>();
                void Visit(ITestAdaptor test)
                {
                    if (!test.IsSuite && test.FullName.StartsWith("DarkNights.Tests.") &&
                        (test.FullName.Contains("MiningToolDefinitionTests") || test.FullName.Contains("MiningSelectorTests") || test.FullName.Contains("ShipTradeTests") || test.FullName.Contains("HeroRecoveryTests") || test.FullName.Contains("HeroBombTests") || test.FullName.Contains("ExpeditionRecoveryTests") || test.FullName.Contains("MiningInputTests") ||
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
            Debug.Log("TOOL_DEFINITION_EDITOR " + status + " count=" + results.Count + " report=" + output);
        }
    }
}
