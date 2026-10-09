using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Diagnostics;
using GameCore.Objects.Runner;
using GameCore.UI.UGUI;
using Newtonsoft.Json;
using Runtime.AppStartup;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor
{
    /// <summary>
    /// 显式执行固定次数的Bootstrap菜单启动采集；单一Editor串行Play，完成后退出。
    /// 用SessionState跨域重载续接有限批次，默认关闭，不更改场景、资产或加载配置。
    /// </summary>
    [InitializeOnLoad]
    public static class BootstrapStartupCapture
    {
        private const string Key = "DarkNights.BootstrapStartupCapture.";
        private static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../artifacts/bootstrap-startup-fix-20261007"));
        private static readonly List<Stamp> Stamps = new List<Stamp>();
        private static long origin;
        private static double next = EditorApplication.timeSinceStartup + 2;
        private static bool written;

        static BootstrapStartupCapture()
        {
            BootstrapStartupTrace.StageRecorded += Record;
            Application.logMessageReceived += OnLog;
            Canvas.willRenderCanvases += OnCanvas;
            EditorApplication.playModeStateChanged += OnPlayState;
            EditorApplication.update += Tick;
        }

        public static void Begin(string variant, int count)
        {
            if (string.IsNullOrEmpty(variant) || variant.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
                throw new ArgumentException("Invalid variant.");
            if (count < 1 || count > 3 || SessionState.GetBool(Key + "Active", false))
                throw new InvalidOperationException("Capture is active or count is invalid.");
            RequireBootstrap();
            for (int i = 0; i < count; i++)
                if (File.Exists(ReportPath(variant, i))) throw new IOException("Capture report already exists.");
            Directory.CreateDirectory(Root);
            SessionState.SetString(Key + "Variant", variant);
            SessionState.SetInt(Key + "Count", count);
            SessionState.SetInt(Key + "Index", 0);
            SessionState.SetBool(Key + "Active", true);
            next = 0;
            Tick();
        }

        private static void RequireBootstrap()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                !scene.IsValid() || scene.path != "Assets/Scenes/Bootstrap.unity" || scene.isDirty)
                throw new InvalidOperationException("Expected saved Bootstrap in idle Edit mode.");
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) Record("EditorEnteredPlay");
            if (state == PlayModeStateChange.EnteredEditMode) next = EditorApplication.timeSinceStartup + 2;
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (message.StartsWith("[AppStartup] Awake initializing Dependency Injection:", StringComparison.Ordinal))
                Record("BootstrapAwake");
            if (message.StartsWith("[AppStartup] Starting '", StringComparison.Ordinal)) Record("BootstrapAsyncStarted");
            if (message == "[AppStartup] Startup completed. Application is ready.") Record("FrameworkReady");
        }

        private static void Record(string stage)
        {
            if (!SessionState.GetBool(Key + "Active", false) || written || Stamps.Any(s => s.Name == stage)) return;
            long now = Stopwatch.GetTimestamp();
            if (origin == 0) origin = now;
            Stamps.Add(new Stamp { Name = stage, Milliseconds = (now - origin) * 1000.0 / Stopwatch.Frequency,
                Frame = Time.frameCount });
        }

        private static void OnCanvas()
        {
            if (Stamps.Any(s => s.Name == "MenuActivated")) Record("FirstMenuCanvasRender");
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key + "Active", false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (EditorApplication.timeSinceStartup < next) return;
                try
                {
                    RequireBootstrap();
                    Stamps.Clear(); origin = 0; written = false;
                    EditorApplication.isPlaying = true;
                }
                catch (Exception error)
                {
                    SessionState.SetBool(Key + "Active", false);
                    File.WriteAllText(Path.Combine(Root, "capture-error.txt"), error.ToString());
                }
                return;
            }
            AppStartup startup = AppStartup.Instance;
            if (startup == null || written) return;
            bool failed = startup.State == AppStartupState.Failed || startup.State == AppStartupState.Cancelled;
            bool timeout = origin != 0 && (Stopwatch.GetTimestamp() - origin) / (double)Stopwatch.Frequency > 30;
            if (failed || timeout || startup.IsReady && Stamps.Any(s => s.Name == "FirstMenuCanvasRender"))
                Finish(startup, !failed && !timeout);
        }

        private static void Finish(AppStartup startup, bool success)
        {
            written = true;
            string variant = SessionState.GetString(Key + "Variant", "unknown");
            int index = SessionState.GetInt(Key + "Index", 0);
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            var ui = UnityEngine.Object.FindAnyObjectByType<DarkNights.Entry.SessionUiController>();
            var panels = UGUIManager.Instance.GetComponentsInChildren<ObjectInstance>(true)
                .Where(panel => panel.Definition != null && panel.Definition.Key.StartsWith("ui.", StringComparison.Ordinal))
                .Select(panel => new { key = panel.Definition.Key, sibling = panel.transform.GetSiblingIndex(),
                    active = panel.gameObject.activeSelf }).ToArray();
            var report = new
            {
                success, variant, run = index + 1, unity = Application.unityVersion,
                simulatedDelay = settings.SimulatedLoadDelay, builder = settings.ActivePlayModeDataBuilder.Name,
                bootstrapToMenuMs = Difference("BootstrapAwake", "FirstMenuCanvasRender"),
                editorEnteredToMenuMs = Difference("EditorEnteredPlay", "FirstMenuCanvasRender"),
                worldResourcesMs = Difference("WorldResourcesStarted", "WorldResourcesReady"),
                uiMs = Difference("UiStarted", "UiReady"),
                uiPreloadMs = Difference("UiPreloadStarted", "UiPreloadReady"),
                sceneMs = Difference("SceneStarted", "SceneReady"),
                stages = Stamps.ToArray(), modules = startup.Results,
                panels, menuPage = ui == null ? "missing" : ui.Page,
                renderBoundary = "First Canvas.willRenderCanvases after MainMenu activation; CPU render preparation, not GPU presentation."
            };
            string path = ReportPath(variant, index);
            File.WriteAllText(path + ".tmp", JsonConvert.SerializeObject(report, Formatting.Indented));
            File.Move(path + ".tmp", path);
            SessionState.SetInt(Key + "Index", index + 1);
            if (!success || index + 1 >= SessionState.GetInt(Key + "Count", 1)) SessionState.SetBool(Key + "Active", false);
            EditorApplication.isPlaying = false;
        }

        private static double? Difference(string start, string end)
        {
            Stamp first = Stamps.FirstOrDefault(s => s.Name == start);
            Stamp last = Stamps.FirstOrDefault(s => s.Name == end);
            return first == null || last == null ? (double?)null : last.Milliseconds - first.Milliseconds;
        }

        private static string ReportPath(string variant, int index) => Path.Combine(Root, variant + "-" + (index + 1).ToString("D2") + ".json");

        /// <summary>一次显式采集的单调时钟阶段点；仅保留名称、CPU时间和Unity帧号。</summary>
        private sealed class Stamp
        {
            public string Name;
            public double Milliseconds;
            public int Frame;
        }
    }
}
