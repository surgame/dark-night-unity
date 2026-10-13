using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DarkNights.Entry;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Lighting;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>用户手动保持 Unity 前台后的有限 Bootstrap ABBA 窗口；监控、OS 前台、真实整帧及照明成本独立记录，保留原后端与场景，不访问用户存档。</summary>
    public sealed class WallDiffuseForegroundBenchmark
    {
        private static WallDiffuseForegroundBenchmark active;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<object> reports = new List<object>();
        private readonly string output, watch;
        private readonly EnvironmentLighting environment;
        private readonly ExplorationLightField original;
        private readonly WallDiffuseMeasuredReferenceField reference;
        private readonly WallDiffuseForegroundBackend backend;
        private readonly PlayerPerformanceCapture frames;
        private int window, phase;
        private double next;
        private Camera camera;
        private readonly SessionNetwork network;
        private readonly HeroPlayerController controller;
        private readonly bool originalReplay;
        private string motion;
        private bool sending;
        private double nextInput;
        private long geometryStart;
        private string boundsStart;
        private bool waitingFocus;
        private double focusDeadline;

        private WallDiffuseForegroundBenchmark(string id)
        {
            output = Path.GetFullPath("../artifacts/light-pruning-20261013/" + id + "/foreground");
            watch = Path.GetFullPath("../artifacts/light-pruning-20261013/" + id + "/memory");
            if (Directory.Exists(output)) throw new InvalidOperationException("保留旧前台证据。");
            WallDiffuseBenchmarkGate.Check(watch, DateTime.UtcNow, System.Diagnostics.Process.GetCurrentProcess().Id);
            network = UnityEngine.Object.FindFirstObjectByType<SessionNetwork>();
            var presentation = UnityEngine.Object.FindFirstObjectByType<HeroLightPresentation>();
            if (!EditorApplication.isPlaying || network?.Client.Ready != true || presentation.SourceCount <= 0)
                throw new InvalidOperationException("需要真实 Bootstrap Ready 且已打开手电。");
            environment = (EnvironmentLighting)typeof(HeroLightPresentation).GetField("field", Private).GetValue(presentation);
            original = typeof(EnvironmentLighting).GetField("backend", Private).GetValue(environment) as ExplorationLightField
                ?? throw new InvalidOperationException("需要当前正式私有后端。");
            reference = new WallDiffuseMeasuredReferenceField(AssetDatabase.LoadAssetAtPath<ComputeShader>(WallDiffusePruningTests.ReferenceShader),
                Core.Logic.Lighting.LightWallDistance.Build, original.Settings, Core.Logic.Lighting.LightBoundaryBuilder.Build);
            backend = new WallDiffuseForegroundBackend(original, reference);
            camera = UnityEngine.Object.FindFirstObjectByType<View.Terrain.TerrainPreview>().ViewCamera;
            controller = network.GetComponent<HeroPlayerController>();
            originalReplay = (bool)typeof(HeroPlayerController).GetField("replayOnly", Private).GetValue(controller);
            typeof(HeroPlayerController).GetField("replayOnly", Private).SetValue(controller, true);
            frames = network.gameObject.AddComponent<PlayerPerformanceCapture>(); frames.Initialize(network);
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "input.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new
            { scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, unity = Application.unityVersion,
                gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                width = Screen.width, height = Screen.height, camera = camera.transform.position.ToString(), camera.orthographicSize,
                sourceLights = presentation.SourceCount, ruleScope = "Actual authored flashlight rules; temporary emitter copies; native device lights retained", settings = original.Settings }, Newtonsoft.Json.Formatting.Indented));
            typeof(EnvironmentLighting).GetField("backend", Private).SetValue(environment, backend);
            BeginWindow();
            waitingFocus = true; focusDeadline = EditorApplication.timeSinceStartup + 60;
        }

        public static void Start(string id)
        {
            if (active != null) throw new InvalidOperationException("前台批次已运行。");
            active = new WallDiffuseForegroundBenchmark(id);
            EditorApplication.update += active.Tick; AssemblyReloadEvents.beforeAssemblyReload += Reloading;
        }
        private static void Reloading() => active?.Finish(new OperationCanceledException("源码导入中断前台批次。"));

        private void BeginWindow()
        {
            int scenario = window / 4; bool optimized = window % 4 == 1 || window % 4 == 2;
            int count = scenario < 5 ? 1 : 4; motion = new[] { "static", "rotate", "move", "dig", "region" }[scenario % 5];
            backend.Select(count, motion, optimized); frames.ResetWindow(); phase = 0; next = EditorApplication.timeSinceStartup + 2;
            var preview = UnityEngine.Object.FindFirstObjectByType<View.Terrain.TerrainPreview>();
            geometryStart = preview.LightingSource.GeometryRevision; boundsStart = preview.LightingLoadedBounds.ToString();
            File.WriteAllText(Path.Combine(output, "status.txt"), window + "/40 " + count + " " + motion + " " + (optimized ? "optimized" : "reference"));
        }

        private void Tick()
        {
            try
            {
                WallDiffuseBenchmarkGate.Check(watch, DateTime.UtcNow, System.Diagnostics.Process.GetCurrentProcess().Id);
                if (waitingFocus)
                {
                    if (!WallDiffuseForegroundBackend.IsForeground())
                    {
                        if (EditorApplication.timeSinceStartup > focusDeadline) throw new TimeoutException("等待用户切到 Unity 前台超时，未采集前台样本。");
                        return;
                    }
                    waitingFocus = false; BeginWindow();
                }
                if (!EditorApplication.isPlaying || !WallDiffuseForegroundBackend.IsForeground()) throw new OperationCanceledException("Unity 已离开前台或退出 Play，本批不签署前台通过。");
                if (EditorApplication.timeSinceStartup >= nextInput && !sending) Input();
                if (EditorApplication.timeSinceStartup < next) return;
                if (phase == 0) { backend.Begin(); phase = 1; next = EditorApplication.timeSinceStartup + 2; return; }
                if (phase == 1) { backend.Pause(); phase = 2; next = EditorApplication.timeSinceStartup + .5; return; }
                var row = backend.End();
                var json = Newtonsoft.Json.Linq.JObject.FromObject(row);
                if ((bool)json["complete"] != true) throw new InvalidOperationException("前台有效 Render 样本不足或窗口未保持前台。");
                frames.Save(Path.Combine(output, "window-" + window + "-whole-frame.json"));
                var preview = UnityEngine.Object.FindFirstObjectByType<View.Terrain.TerrainPreview>();
                reports.Add(new { window, lighting = row, wholeFrameFile = "window-" + window + "-whole-frame.json", wholeFrameMedianMs = Median(frames),
                    geometryStart, geometryEnd = preview.LightingSource.GeometryRevision, boundsStart, boundsEnd = preview.LightingLoadedBounds.ToString(),
                    camera = camera.transform.position.ToString(), width = Screen.width, height = Screen.height });
                if (window == 0 || window == 20) ScreenCapture.CaptureScreenshot(Path.Combine(output, "window-" + window + ".png"));
                if (++window == 40) Finish(null); else BeginWindow();
            }
            catch (Exception error) { Finish(error); }
        }
        private async void Input()
        {
            sending = true; nextInput = EditorApplication.timeSinceStartup + .1;
            try
            {
                var hero = controller.Current ?? throw new InvalidOperationException("前台输入的主角尚未 Ready。");
                HeroMiningTarget target = default;
                if (motion == "dig")
                {
                    var map = network.Terrain.Replica;
                    var tool = network.ObjectWorld.Resources.Equipment.Mining(hero.Slot0Definition);
                    if (TerrainMiningQuery.FirstSurface(map, hero.X, hero.Height + tool.HandHeight, 0, -1, tool.Reach, out var cell, out _))
                    {
                        var value = map.Read(cell).Cell;
                        target = new HeroMiningTarget(map.World.WorldId.ToString().Replace("-", ""), map.World.Epoch, cell.U, cell.V,
                            value.TileId, value.Flags, HeroMiningTargetKind.Foreground, 0, map.ContentVersion(cell), 0);
                    }
                }
                await network.Client.SendInput(hero.Id, hero.ControlLease, motion == "region" ? (window % 2 == 0 ? 1 : -1) : 0,
                    motion == "region", motion == "dig", jumpPressed: motion == "region", aimAngle: -90,
                    selectionRevision: hero.SelectionRevision, mining: target, lightAimAngle: -90);
            }
            catch (Exception error) { if (active == this) Finish(error); }
            finally { sending = false; }
        }
        private static object Median(PlayerPerformanceCapture capture)
        {
            var values = typeof(PlayerPerformanceCapture).GetField("foregroundFrames", Private).GetValue(capture);
            return values.GetType().GetMethod("Percentile").Invoke(values, new object[] { .5 });
        }
        private void Finish(Exception error)
        {
            EditorApplication.update -= Tick; AssemblyReloadEvents.beforeAssemblyReload -= Reloading;
            backend.Pause(); typeof(EnvironmentLighting).GetField("backend", Private).SetValue(environment, original);
            typeof(HeroPlayerController).GetField("replayOnly", Private).SetValue(controller, originalReplay);
            backend.Dispose(); reference.RetireAsync(); UnityEngine.Object.Destroy(frames);
            File.WriteAllText(Path.Combine(output, "result.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new
            { complete = error == null, valid = error == null, error = error?.ToString(), reports,
                scope = "Actual foreground Bootstrap, 1/4 authored flashlight copies plus native device lights; static/rotate/move and Gateway-driven dig/region. Dynamic world changes are recorded per window, not identical replayed worlds. CPU/GPU lighting windows and OS-validated whole game frame intervals; no Player or multiplayer performance claim." }, Newtonsoft.Json.Formatting.Indented));
            active = null;
        }
    }
}
