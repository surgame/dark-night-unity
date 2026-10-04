using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>洞口分层素材的有限验证批次；只运行受影响算法、资源与真实相机检查，原子保存结果，不自动重复提交。</summary>
    public sealed class CaveEntranceArtValidation : ICallbacks
    {
        private static CaveEntranceArtValidation active;
        private TestRunnerApi api;
        private string output;
        private readonly JArray results = new JArray();
        public static string Root => Path.GetFullPath("../artifacts/art-layer-entrance-20261003");
        [DarkNightsWorkbenchCommand("Dark Nights/Verify/Cave Entrance Art Tests", "洞口美术检查")]
        public static void RunMenu() => Run();
        public static void Run(string filter = null)
        {
            if (active != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Editor 尚有活动任务。");
            Directory.CreateDirectory(Root);
            var batch = active = new CaveEntranceArtValidation();
            batch.output = Path.Combine(Root, "editor-" + DateTime.Now.ToString("HHmmss") + ".json");
            batch.api = ScriptableObject.CreateInstance<TestRunnerApi>(); batch.api.RegisterCallbacks(batch);
            batch.api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                testNames = filter != null ? filter.Split('|') : new[] { "DarkNights.Tests.CaveEntranceArtTests", "DarkNights.Tests.CaveEntranceArtGpuTests",
                    "DarkNights.Tests.BackgroundReferenceTests", "DarkNights.Tests.SurfaceEnvironmentTests", "DarkNights.Tests.SurfaceSkyTests" } }));
        }
        public static void BuildMono(string label = "")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Editor 有活动任务。");
            if (label.Any(c => !char.IsLetterOrDigit(c) && c != '-')) throw new ArgumentException("构建标签无效。");
            string suffix = label.Length == 0 ? "" : "-" + label;
            string output = Path.Combine(Root, "player-mono" + suffix + "/DarkNights.exe"), report = Path.Combine(Root, "build" + suffix + ".json");
            if (Directory.Exists(Path.GetDirectoryName(output)) || File.Exists(report)) throw new IOException("构建目录已有产物，不能覆盖。");
            var result = new JObject { ["backend"] = "Mono", ["startedUtc"] = DateTime.UtcNow.ToString("O") };
            try
            {
                typeof(GamePlayerBuild).GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { ScriptingImplementation.Mono2x, "mono", BuildOptions.Development, output });
                result["success"] = true; result["player"] = output;
            }
            catch (Exception error) { result["success"] = false; result["error"] = error.ToString(); }
            result["finishedUtc"] = DateTime.UtcNow.ToString("O");
            File.WriteAllText(report + ".tmp", result.ToString()); File.Move(report + ".tmp", report);
        }
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.Test.IsSuite) results.Add(new JObject { ["name"] = result.Test.FullName,
                ["status"] = result.TestStatus.ToString(), ["message"] = result.Message, ["stack"] = result.StackTrace });
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            bool success = results.Count > 0 && results.All(r => (string)r["status"] == "Passed");
            File.WriteAllText(output + ".tmp", new JObject { ["success"] = success, ["results"] = results }.ToString());
            File.Move(output + ".tmp", output); api.UnregisterCallbacks(this); UnityEngine.Object.DestroyImmediate(api); active = null;
            Debug.Log("CAVE_ENTRANCE_ART_TESTS success=" + success + " count=" + results.Count + " report=" + output);
        }
    }
}
