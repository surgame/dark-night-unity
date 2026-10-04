using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>显式提交地表环境的简单 Editor 验证；仅运行专项纯计算和相机渲染，不构建 Player、不启动第二个 Editor。</summary>
    [InitializeOnLoad]
    public sealed class SurfaceEnvironmentValidation : ICallbacks
    {
        private static SurfaceEnvironmentValidation active;
        private TestRunnerApi api;
        private string run, output;
        private double deadline;
        private readonly JArray results = new JArray();
        public static string EvidenceRoot => Path.GetFullPath("../artifacts/surface-environment-20261001");
        private static readonly double RequestDeadline;
        static SurfaceEnvironmentValidation()
        {
            // 仅消费本批明确写入的单次请求；没有文件时不注册轮询，不自动重复提交。
            if (!File.Exists(Path.Combine(EvidenceRoot, "request-tests.txt"))) return;
            RequestDeadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update += SubmitRequested;
        }
        private static void SubmitRequested()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (EditorApplication.timeSinceStartup < RequestDeadline) return;
                EditorApplication.update -= SubmitRequested; return;
            }
            EditorApplication.update -= SubmitRequested;
            string request = Path.Combine(EvidenceRoot, "request-tests.txt");
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Move(request, Path.Combine(EvidenceRoot, "submitted-" + DateTime.Now.ToString("HHmmss") + ".txt"));
            Run();
        }

        [DarkNightsWorkbenchCommand("Dark Nights/Verify/Surface Environment Tests", "地表环境检查")]
        public static void Run()
        {
            if (active != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Editor 尚有活动任务。");
            Directory.CreateDirectory(EvidenceRoot);
            var batch = active = new SurfaceEnvironmentValidation();
            batch.output = Path.Combine(EvidenceRoot, "editor-" + DateTime.Now.ToString("HHmmss") + ".json");
            batch.api = ScriptableObject.CreateInstance<TestRunnerApi>(); batch.api.RegisterCallbacks(batch);
            batch.deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += batch.Watch;
            batch.run = batch.api.Execute(new ExecutionSettings(new Filter
            { testMode = TestMode.EditMode, testNames = new[] { "DarkNights.Tests.SurfaceSkyTests", "DarkNights.Tests.SurfaceEnvironmentTests" } }));
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
            File.WriteAllText(output, new JObject { ["status"] = status, ["results"] = results }.ToString());
            UnityEngine.Object.DestroyImmediate(api);
            Debug.Log("SURFACE_ENVIRONMENT_EDITOR " + status + " count=" + results.Count + " report=" + output);
        }
    }
}
