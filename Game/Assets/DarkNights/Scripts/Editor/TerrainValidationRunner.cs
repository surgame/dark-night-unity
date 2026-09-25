using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>已启动 Editor 的串行地形验收入口。只接受显式测试、状态和构建请求；结果保留在独立 artifacts 目录。</summary>
    [InitializeOnLoad]
    public static class TerrainValidationRunner
    {
        private static readonly string Root = Path.GetFullPath("../artifacts/terrain-final-20260926");
        private static TestRunnerApi api;
        private static double next;
        private const string ActiveKey = "DarkNights.TerrainValidation.Active";
        private static string active;
        static TerrainValidationRunner()
        {
            EditorApplication.delayCall += Initialize;
        }
        private static void Initialize()
        {
            Directory.CreateDirectory(Root);
            active = SessionState.GetString(ActiveKey, "");
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results());
            EditorApplication.update += Tick;
            WriteStatus("ready");
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            WriteStatus(active.Length == 0 ? "idle" : "running");
            string requestPath = Path.Combine(Root, "request.json");
            if (!File.Exists(requestPath) || active.Length != 0) return;
            Request request = JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));
            if (request == null || string.IsNullOrEmpty(request.id) || request.id.Any(c => !char.IsLetterOrDigit(c) && c != '-')) return;
            File.Move(requestPath, Path.Combine(Root, request.id + ".request.json"));
            try
            {
                if (request.command == "status") return;
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play before a new validation batch.");
                if (EditorSceneManager.GetSceneManagerSetup().Any(scene => UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty))
                    throw new InvalidOperationException("Unsaved scene detected; validation did not change scenes.");
                active = request.id; SessionState.SetString(ActiveKey, active);
                if (request.command == "tests")
                {
                    var mode = request.mode == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode;
                    var filter = new Filter { testMode = mode, assemblyNames = request.assemblies, testNames = request.tests };
                    api.Execute(new ExecutionSettings(filter));
                }
                else if (request.command == "build")
                {
                    GamePlayerBuild.MapStateMono();
                    Complete("BUILD_RETURNED", 0, 0, 0);
                }
                else throw new ArgumentException("Unsupported validation command.");
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Root, request.id + ".error.txt"), error.ToString());
                Complete("ERROR", 0, 1, 0);
            }
        }
        private static void Complete(string state, int passed, int failed, int skipped)
        {
            var summary = new Summary { id = active, state = state, passed = passed, failed = failed, skipped = skipped,
                utc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion };
            File.WriteAllText(Path.Combine(Root, active + ".summary.json"), JsonUtility.ToJson(summary, true));
            active = ""; SessionState.SetString(ActiveKey, "");
        }
        private static void WriteStatus(string phase)
        {
            var status = new Status { phase = phase, active = active, playing = EditorApplication.isPlaying,
                utc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion,
                scenes = EditorSceneManager.GetSceneManagerSetup().Select(scene => scene.path).ToArray() };
            File.WriteAllText(Path.Combine(Root, "editor-status.json"), JsonUtility.ToJson(status, true));
        }
        /// <summary>有限测试请求，不允许注入脚本或任意文件路径。</summary>
        [Serializable] private sealed class Request
        {
            public string id, command, mode;
            public string[] assemblies, tests;
        }
        /// <summary>Editor 运行状态，不包含授权信息。</summary>
        [Serializable] private sealed class Status
        {
            public string phase, active, utc, unity;
            public bool playing;
            public string[] scenes;
        }
        /// <summary>一次批次的真实结果计数。</summary>
        [Serializable] private sealed class Summary
        {
            public string id, state, utc, unity;
            public int passed, failed, skipped;
        }
        /// <summary>Test Runner 回调在域重载后重新注册，不重复发起同一测试。</summary>
        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test)
            {
                if (active.Length != 0) File.WriteAllText(Path.Combine(Root, "current-test.txt"), test.FullName);
            }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (active.Length == 0) return;
                TestRunnerApi.SaveResultToFile(result, Path.Combine(Root, active + ".xml"));
                Complete(result.ResultState, result.PassCount, result.FailCount, result.SkipCount);
            }
        }
    }
}
