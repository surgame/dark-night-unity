using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>在当前单一 Editor 中执行本批小范围 EditMode 测试；结果写入独立证据目录，不启动第二个 Unity。</summary>
    public sealed class ShipTradeQuickRunner : ICallbacks
    {
        private static ShipTradeQuickRunner active;
        private readonly string root = Path.GetFullPath("../artifacts/ship-trade-20260929");
        private TestRunnerApi api;
        private string run;
        private double deadline;
        private int failed;

        [InitializeOnLoadMethod]
        private static void RunRequestedBatch()
        {
            if (File.Exists(Path.GetFullPath("Library/ShipTradeQuick.request")))
                EditorApplication.update += CheckRequest;
        }

        private static void CheckRequest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            EditorApplication.update -= CheckRequest;
            string request = Path.GetFullPath("Library/ShipTradeQuick.request");
            if (!File.Exists(request)) return;
            File.Delete(request);
            Run();
        }

        [MenuItem("Dark Nights/Verify/Run Ship Trade Quick Tests")]
        public static void Run()
        {
            if (active != null || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("退出 Play 并等待已有测试结束。");
            var batch = active = new ShipTradeQuickRunner();
            Directory.CreateDirectory(batch.root);
            File.WriteAllText(Path.Combine(batch.root, "editor-tests.status"), "running");
            batch.api = ScriptableObject.CreateInstance<TestRunnerApi>();
            batch.api.RegisterCallbacks(batch);
            string[] tests =
            {
                "DarkNights.Tests.ShipTradeTests.TradeValuesAndSprintBoundsAreValidated",
                "DarkNights.Tests.ShipTradeTests.EmptyEquipmentAndSprintAreAuthoritative",
                "DarkNights.Tests.ShipTradeTests.SaveRejectsInvalidEquipmentAndCredits",
                "DarkNights.Tests.ShipTradeTests.ShopReservesFirstPickaxeBudget",
                "DarkNights.Tests.ShipTradeTests.ShopAndSaleUseLeasePositionAndAtomicState",
                "DarkNights.Tests.HeroControlTests.SelectionVersionAndFuelRemainAuthoritative",
                "DarkNights.Tests.HeroControlTests.PickaxeOnlyAnimatesWithoutAssigningWorkOrProducingResources",
                "DarkNights.Tests.HeroRecoveryTests.AirborneRecoveryKeepsMotionAndEquipmentButReleasesOwnership",
                "DarkNights.Tests.HeroBombTests.ThrowConsumesOneChargeAndEmptyInventoryCannotStartCharging",
                "DarkNights.Tests.ExpeditionTests.StarterRouteUsesFiniteFuelAndMiningKeepsOreIndependent"
            };
            batch.deadline = EditorApplication.timeSinceStartup + 180;
            batch.run = batch.api.Execute(new ExecutionSettings(new Filter
            { testMode = TestMode.EditMode, testNames = tests }));
            EditorApplication.update += batch.CheckTimeout;
        }

        private void CheckTimeout()
        {
            if (EditorApplication.timeSinceStartup < deadline) return;
            TestRunnerApi.CancelTestRun(run);
            Finish("timeout", 0, failed, 0, null);
        }

        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test)
        {
            if (!test.IsSuite) File.WriteAllText(Path.Combine(root, "editor-tests.current"), test.FullName);
        }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.Test.IsSuite || result.TestStatus != TestStatus.Failed) return;
            failed++;
            File.AppendAllText(Path.Combine(root, "editor-tests.failures.txt"),
                result.Test.FullName + System.Environment.NewLine + result.Message + System.Environment.NewLine);
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, Path.Combine(root, "editor-tests.xml"));
            Finish(result.ResultState, result.PassCount, result.FailCount, result.SkipCount, result);
        }

        private void Finish(string state, int passed, int failures, int skipped, ITestResultAdaptor result)
        {
            if (active != this) return;
            EditorApplication.update -= CheckTimeout;
            File.WriteAllText(Path.Combine(root, "editor-tests.status"),
                $"{state} passed={passed} failed={failures} skipped={skipped} utc={DateTime.UtcNow:o}");
            api.UnregisterCallbacks(this);
            active = null;
        }
    }
}
