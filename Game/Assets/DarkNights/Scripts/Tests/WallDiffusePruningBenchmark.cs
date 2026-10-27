using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Editor.Lighting;
using DarkNights.Entry;
using DarkNights.Tests.LightingPruningBaseline;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>显式有限的作者五图与 ABBA 性能对照；读取当前手电参数，两种光形逐像素比较，再串行计量静态／动态及参数成本，不签署前台或 Player 性能。</summary>
    public sealed class WallDiffusePruningBenchmark
    {
        private static WallDiffusePruningBenchmark active;
        private readonly string output;
        private readonly bool focused;
        private readonly int samples;
        private readonly string watch;
        private readonly List<object> reports = new List<object>();
        private readonly List<LightEmitterData> emitters = new List<LightEmitterData>(16);
        private readonly WallDiffuseBenchmarkStage stage = new WallDiffuseBenchmarkStage();
        private readonly WallDiffuseGpuMeasurement measurement = new WallDiffuseGpuMeasurement();
        private readonly LightEmissionRules original;
        private ExplorationLightField candidate;
        private WallDiffuseMeasuredReferenceField reference;
        private IEnumerator routine;
        private IEnumerator pendingMeasurement;
        private int frame;

        private WallDiffusePruningBenchmark(string path, string watch, bool focused, int samples)
        {
            output = path; this.watch = watch; this.focused = focused; this.samples = samples;
            var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/Flashlight/Flashlight.asset");
            original = LightProfileResolver.Resolve(LightDefinitionEditor.Preset(definition), LightDefinitionEditor.Config(definition).Overrides, LightDefinitionEditor.DefaultTemplate).Rules;
            File.WriteAllText(Path.Combine(output, "input.json"), Newtonsoft.Json.JsonConvert.SerializeObject(original, Newtonsoft.Json.Formatting.Indented));
        }

        public static void Start(string id, bool focused = false, int samples = 20)
        {
            if (active != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("需要空闲的单一 Editor。");
            if (string.IsNullOrEmpty(id) || id.Any(c => !char.IsLetterOrDigit(c) && c != '-')) throw new ArgumentException("批次标识非法。");
            if (samples < 16 || samples > 80) throw new ArgumentOutOfRangeException(nameof(samples));
            if (!focused && samples > 50) throw new ArgumentOutOfRangeException(nameof(samples), "完整矩阵最多4000个预注册 GPU 标记。");
            string root = Path.GetFullPath("../artifacts/light-pruning-20261013/" + id);
            string watch = Path.Combine(root, "memory");
            WallDiffuseBenchmarkGate.Check(watch, DateTime.UtcNow, System.Diagnostics.Process.GetCurrentProcess().Id);
            string path = Path.Combine(root, "benchmark");
            if (Directory.Exists(path)) throw new InvalidOperationException("不覆盖旧性能证据。");
            Directory.CreateDirectory(path); active = new WallDiffusePruningBenchmark(path, watch, focused, samples); active.routine = active.Run();
            EditorApplication.update += active.Tick; AssemblyReloadEvents.beforeAssemblyReload += Reloading;
        }

        private static void Reloading() => active?.Finish(new OperationCanceledException("源码导入中断性能批次，保留已有样本。"));
        private void Tick()
        {
            try
            {
                WallDiffuseBenchmarkGate.Check(watch, DateTime.UtcNow, System.Diagnostics.Process.GetCurrentProcess().Id);
                if (!routine.MoveNext()) Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }

        private void Finish(Exception error)
        {
            EditorApplication.update -= Tick; AssemblyReloadEvents.beforeAssemblyReload -= Reloading;
            (pendingMeasurement as IDisposable)?.Dispose(); (routine as IDisposable)?.Dispose();
            candidate?.RetireAsync(); reference?.RetireAsync(); stage.Dispose(); measurement.Dispose(); ExplorationLightField.Suspend();
            File.WriteAllText(Path.Combine(output, "result.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new
            { complete = error == null, valid = error == null, error = error?.ToString(), monitor = watch,
                scope = "Single Editor; actual terrain and lighting shaders; GPU marker + completion fence; foreground/Player not measured", reports }, Newtonsoft.Json.Formatting.Indented));
            active = null;
        }

        private IEnumerator Run()
        {
            foreach (string map in focused ? new[] { "switchback" } : DarkNights.Editor.Lighting.LightingPreviewMap.Ids)
            {
                Status(map + " loading"); stage.Open(map); double deadline = EditorApplication.timeSinceStartup + 120;
                while (!stage.Ready)
                {
                    stage.Tick(); if (stage.Error != null) throw stage.Error;
                    if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("地图准备超时。"); yield return null;
                }
                var profile = AssetDatabase.LoadAssetAtPath<SceneLightingProfile>("Assets/DarkNights/Res/Shared/Lighting/Profiles/PinewatchLighting.asset");
                candidate = new ExplorationLightField(AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/DarkNights/Res/Shared/Lighting/ExplorationLighting.compute"),
                    DarkNights.Core.Logic.Lighting.LightWallDistance.Build, profile.Settings, DarkNights.Core.Logic.Lighting.LightBoundaryBuilder.Build);
                reference = new WallDiffuseMeasuredReferenceField(AssetDatabase.LoadAssetAtPath<ComputeShader>(WallDiffusePruningTests.ReferenceShader),
                    DarkNights.Core.Logic.Lighting.LightWallDistance.Build, profile.Settings, DarkNights.Core.Logic.Lighting.LightBoundaryBuilder.Build);
                foreach (var shape in focused ? new[] { WallDiffuseShape.SoftBlob } : new[] { WallDiffuseShape.WallBand, WallDiffuseShape.SoftBlob })
                {
                    var rules = Rules(shape, "current"); Emit(rules, 1, 0);
                    for (int warm = 0; warm < 32; warm++) { Render(reference); Render(candidate); yield return null; }
                    if (candidate.DiffuseResources.CandidateCount <= 0 || reference.DiffuseResources.CandidateCount <= 0) throw new InvalidOperationException("对照姿态未产生候选，拒绝空测。");
                    Render(reference); var rgb = WallDiffuseBenchmarkStage.Pixels(Shader.GetGlobalTexture("_DNLightField"));
                    var directions = WallDiffuseBenchmarkStage.Pixels(Shader.GetGlobalTexture("_DNLightDirection")); stage.Save(Path.Combine(output, map + "-" + shape + "-reference.png"));
                    Render(candidate); float rgbError = WallDiffusePruningTests.Maximum(rgb, WallDiffuseBenchmarkStage.Pixels(Shader.GetGlobalTexture("_DNLightField")));
                    float directionError = WallDiffusePruningTests.Maximum(directions, WallDiffuseBenchmarkStage.Pixels(Shader.GetGlobalTexture("_DNLightDirection")));
                    stage.Save(Path.Combine(output, map + "-" + shape + "-optimized.png"));
                    reports.Add(new { map, shape = shape.ToString(), kind = "author-pixels", rgbError, directionError, mouth = stage.Mouth.ToString(), angle = stage.Angle });
                    if (rgbError > .00003f || directionError > .00003f) throw new InvalidOperationException("作者地图画面对照不一致。");
                    if (map != "switchback") continue;
                    if (focused)
                    {
                        foreach (string motion in new[] { "region", "static", "rotate", "dig" })
                        {
                            var probe = Case(map, shape, rules, 4, motion, "current", samples);
                            while (probe.MoveNext()) yield return probe.Current;
                        }
                        continue;
                    }
                    foreach (int lights in new[] { 1, 4 }) foreach (string motion in new[] { "static", "rotate", "move", "dig", "region" })
                    {
                        var test = Case(map, shape, rules, lights, motion, "current", samples);
                        while (test.MoveNext()) yield return test.Current;
                    }
                }
                var retire = candidate.RetireAsync(); while (!retire.IsCompleted) yield return null; retire.GetAwaiter().GetResult(); candidate = null;
                retire = reference.RetireAsync(); while (!retire.IsCompleted) yield return null; retire.GetAwaiter().GetResult(); reference = null;
                stage.Dispose();
            }
        }

        private IEnumerator Case(string map, WallDiffuseShape shape, LightEmissionRules rules, int count, string motion, string variant, int samples)
        {
            stage.ResetCamera(); Emit(rules, count, 0);
            for (int warm = 0; warm < 12; warm++) { Render(reference); Render(candidate); yield return null; }
            var before = candidate.DiffuseResources.PreparationCount;
            for (int sample = 0; sample < samples; sample++)
            {
                int block = 0;
                foreach (string backend in new[] { "reference", "optimized", "optimized", "reference" })
                {
                    bool optimized = backend == "optimized";
                    Action draw = () => (optimized ? (IEnvironmentLightBackend)candidate : reference).Render(stage.Camera, stage.Terrain, emitters);
                    Status(map + " " + shape + " " + count + " " + motion + " " + variant + " " + backend + " " + frame);
                    stage.ResetCamera();
                    if (motion == "region") stage.Region(sample);
                    if (motion == "dig") stage.Dig(sample + 1);
                    Emit(rules, count, motion == "rotate" ? sample * 1.5f : 0);
                    double settledDeadline = EditorApplication.timeSinceStartup + 30;
                    do
                    {
                        stage.Tick(); Render(reference); Render(candidate); yield return null;
                        if (EditorApplication.timeSinceStartup > settledDeadline) throw new TimeoutException("相同输入的缓存就绪屏障超时。");
                    } while (!stage.Ready || !reference.GeometrySettled || !candidate.GeometrySettled || !reference.BoundarySettled || !candidate.DiffuseResources.Settled);
                    long tickStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    frame++; if (motion == "dig") stage.Dig(sample); else if (motion == "region") stage.Region(sample + 1);
                    Emit(rules, count, motion == "rotate" ? (sample + 1) * 1.5f : 0);
                    if (motion == "move") for (int i = 0; i < emitters.Count; i++)
                        emitters[i] = new LightEmitterData(emitters[i].Position + new Vector3(sample % 2 == 0 ? .25f : -.25f, 0, 0), emitters[i].Angle, rules);
                    stage.Terrain.TickFromEditor();
                    double terrainTickMs = (System.Diagnostics.Stopwatch.GetTimestamp() - tickStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    var measured = measurement.Run(draw, marker => { if (optimized) candidate.MeasurementSampler = marker; else reference.MeasurementSampler = marker; });
                    pendingMeasurement = measured;
                    while (measured.MoveNext()) yield return measured.Current;
                    (measured as IDisposable)?.Dispose(); pendingMeasurement = null;
                    reports.Add(new { map, shape = shape.ToString(), lights = count, motion, variant, backend, frame,
                        block, sample, terrainTickMs, missingGpuReason = measurement.MissingGpuReason,
                        cpuMs = measurement.CpuMilliseconds, gpuNs = measurement.GpuNanoseconds, gcBytes = measurement.GcBytes, gcEvents = measurement.GcEvents,
                        receipt = measurement.Receipt, preparationCount = candidate.DiffuseResources.PreparationCount, preparationBeforeCase = before,
                        candidates = optimized ? candidate.DiffuseResources.CandidateCount : reference.DiffuseResources.CandidateCount,
                        uploadedBytes = optimized ? candidate.DiffuseResources.UploadedBytes : reference.DiffuseResources.UploadedBytes,
                        freezeMs = optimized ? candidate.DiffuseResources.FreezeMilliseconds : reference.DiffuseResources.FreezeMilliseconds,
                        geometryTickMs = optimized ? candidate.GeometryTickMilliseconds : reference.GeometryTickMilliseconds,
                        boundaryTickMs = optimized ? candidate.DiffuseResources.BoundaryTickMilliseconds : 0,
                        candidateMs = optimized ? candidate.DiffuseResources.CandidateMilliseconds : 0,
                        prepareMs = optimized ? candidate.DiffuseResources.PrepareMilliseconds : reference.DiffuseResources.PrepareMilliseconds,
                        uploadMs = optimized ? candidate.DiffuseResources.UploadMilliseconds : reference.DiffuseResources.UploadMilliseconds,
                        submitMs = optimized ? candidate.SubmitMilliseconds : reference.SubmitMilliseconds });
                    block++;
                }
            }
            if (motion == "dig") stage.Dig(1);
            stage.ResetCamera(); Emit(rules, count, 0);
            for (int settle = 0; settle < 24; settle++) { Render(reference); Render(candidate); yield return null; }
            Render(reference); var originalPixels = WallDiffuseBenchmarkStage.Pixels(Shader.GetGlobalTexture("_DNLightField"));
            Render(candidate); float maximumError = WallDiffusePruningTests.Maximum(originalPixels, WallDiffuseBenchmarkStage.Pixels(Shader.GetGlobalTexture("_DNLightField")));
            reports.Add(new { map, shape = shape.ToString(), lights = count, motion, variant, kind = "after-case-pixels", maximumError });
            if (maximumError > .00003f) throw new InvalidOperationException("动态／参数切换后的画面不一致。");
        }

        private LightEmissionRules Rules(WallDiffuseShape shape, string variant)
        {
            var b = original.Bounce;
            var bounce = new LightBounceRules(LightBounceMode.Continuous, b.Bounce, shape,
                variant == "unmatched" ? false : b.DiffuseMatchIncident, variant == "corner0" ? 0 : b.DiffuseCornerRadius,
                b.DiffuseGain, variant == "near1.5" ? 1.5f : b.DiffuseNearDistance, b.DiffuseFarDistance,
                variant == "reach1" ? 1 : b.DiffuseMaxReach, variant == "spread2" ? 2 : b.DiffuseLateralSpread,
                variant == "probes4" ? 4 : b.DiffuseVisibilityProbes);
            return new LightEmissionRules(original.Range, original.Cone, original.Intensity, original.NearRange, original.NearIntensity,
                original.Red, original.Green, original.Blue, original.ApertureWidth, variant == "hard-shadow" ? false : original.SoftShadows,
                original.Softness, original.ConeFeather, bounce);
        }

        private void Emit(LightEmissionRules rules, int count, float turn)
        {
            emitters.Clear();
            for (int i = 0; i < count; i++) emitters.Add(new LightEmitterData(new Vector3(stage.Mouth.x - .5f, -stage.Mouth.y + .5f, 0), stage.Angle + turn + i * 2, rules));
        }
        private void Render(IEnvironmentLightBackend backend) { stage.Terrain.TickFromEditor(); backend.Render(stage.Camera, stage.Terrain, emitters); }
        private void Status(string state) => File.WriteAllText(Path.Combine(output, "status.txt"), state);
    }
}
