using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Lighting;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Editor.Terrain;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Lighting;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace DarkNights.Editor.Lighting
{
    /// <summary>显式、有限的五图离屏照明验收；复用单 Editor 和正式 TerrainPreview，保留原始计时／图片，后台结果不签署前台性能。</summary>
    public sealed class WallDiffuseAcceptance
    {
        private static WallDiffuseAcceptance active;
        private readonly string output;
        private readonly bool dynamicOnly;
        private readonly List<object> reports = new List<object>();
        private readonly TerrainStylePreviewStage stage = new TerrainStylePreviewStage();
        private readonly TerrainStyleDrafts drafts = new TerrainStyleDrafts();
        private readonly List<LightEmitterData> emitters = new List<LightEmitterData>(16);
        private ExplorationLightField field;
        private ExplorationLightField baseline;
        private IEnumerator run;
        private bool wasProfiling;
        private Unity.Profiling.ProfilerRecorder gc;
        private Camera camera;
        private TerrainPreview preview;
        private Vector2 mouth;
        private float angle;
        private TerrainBlueprint blueprint;

        private WallDiffuseAcceptance(string path, bool dynamicOnly) { output = path; this.dynamicOnly = dynamicOnly; }
        public static void Start(string id, bool dynamicOnly = false)
        {
            if (active != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("验收需要空闲的单一 Editor。");
            if (id.Any(c => !char.IsLetterOrDigit(c) && c != '-')) throw new ArgumentException("批次标识非法。");
            string path = Path.GetFullPath("../artifacts/wall-diffuse-20261012/" + id);
            if (Directory.Exists(path)) throw new InvalidOperationException("保留已有验收目录，请使用新的批次标识。");
            Directory.CreateDirectory(path); active = new WallDiffuseAcceptance(path, dynamicOnly);
            active.wasProfiling = Profiler.enabled; Profiler.enabled = true;
            active.gc = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC.Alloc", 256,
                Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread | Unity.Profiling.ProfilerRecorderOptions.StartImmediately);
            var positiveControl = new byte[8192]; GC.KeepAlive(positiveControl); active.gc.Stop();
            if (!active.gc.Valid || active.gc.Count == 0) { active.gc.Dispose(); Profiler.enabled = active.wasProfiling; active = null; throw new InvalidOperationException("GC.Alloc 记录器未通过已知分配自检。"); }
            active.run = active.Run(); EditorApplication.update += active.Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Reloading;
        }

        private void Tick()
        {
            try
            {
                if ((File.Exists(Path.GetFullPath("../artifacts/wall-diffuse-20261012/memory-tests/breach.json")) || File.Exists(Path.GetFullPath("../artifacts/wall-diffuse-20261012/memory-dynamic/breach.json"))))
                    throw new OperationCanceledException("内存门控触发，停止本任务验收。");
                if (!run.MoveNext()) Complete(null);
            }
            catch (Exception error) { Complete(error); }
        }
        private void Complete(Exception error)
        {
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Reloading;
            ExplorationLightField.Suspend(); if (field != null) field.RetireAsync(); field = null;
            if (baseline != null) baseline.RetireAsync(); baseline = null;
            stage.Dispose(); drafts.Dispose(); gc.Dispose(); Profiler.enabled = wasProfiling;
            File.WriteAllText(Path.Combine(output, "result.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new
            { complete = error == null, error = error?.ToString(), scope = "Single Editor, actual terrain/shaders, offscreen; foreground performance pending", reports }, Newtonsoft.Json.Formatting.Indented));
            active = null;
        }

        private static void Reloading() => active?.Complete(new OperationCanceledException("源码导入前保留未完成验收回执。"));

        private IEnumerator Run()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/DarkNights/Res/Shared/Lighting/ExplorationLighting.compute");
            var definition = AssetDatabase.LoadAssetAtPath<AnyRules.Next.Authoring.ARDMapDefinition>("Assets/DarkNights/Res/Terrain/StrataCave/MapDefinition.asset");
            var style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>("Assets/DarkNights/Res/Terrain/StrataCave/Style.asset");
            foreach (string map in dynamicOnly ? new[] { "folded-expanded" } : new[] { "rift-expanded", "karst-expanded", "folded-expanded", "shaft-loop-expanded", "vein-maze-expanded" })
            {
                File.WriteAllText(Path.Combine(output, "status.txt"), map + " loading");
                var author = AssetDatabase.LoadAssetAtPath<MineMapAsset>("Assets/DarkNights/Res/Terrain/MineMaps/" + map + ".minemap");
                blueprint = MineMapGenerator.Generate(new TerrainGenerationSettings { ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile, MineMap = author.Freeze() });
                Pose(author.Freeze());
                stage.Open(definition, blueprint, style, drafts, blueprint.CopyMaterials(), blueprint.CopyShapes(),
                    new BackgroundBakeDescriptor(Guid.NewGuid().ToString("N"), blueprint.Settings.Seed, blueprint.CopyMaterials(), blueprint.CopyShapes()));
                camera = Read<Camera>(stage, "camera"); preview = Read<TerrainPreview>(stage, "terrain");
                camera.transform.position = new Vector3(mouth.x - .5f, -mouth.y + .5f, -10); camera.orthographicSize = 6;
                double deadline = EditorApplication.timeSinceStartup + 120;
                while (!stage.Ready)
                {
                    stage.Tick(); if (stage.Error != null) throw stage.Error;
                    if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(stage.Progress);
                    yield return null;
                }
                var settings = new ExplorationLightSettings { BounceMode = LightBounceMode.Continuous, Ambient = .1f };
                field = new ExplorationLightField(shader, LightWallDistance.Build, settings, LightBoundaryBuilder.Build);
                baseline = new ExplorationLightField(AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/DarkNights/Scripts/Tests/WallDiffuseLegacyBaseline.compute"),
                    LightWallDistance.Build, new ExplorationLightSettings());
                foreach (string name in new[] { "DN.Light.Total", "DN.Diffuse.Profile", "DN.Diffuse.Select", "DN.Diffuse.IntegrateCompose", "DN.Direct.Legacy" }) Recorder.Get(name).enabled = true;
                Emit(1, 0);
                for (int i = 0; i < 60; i++)
                {
                    gc.Reset(); gc.Start(); long coldStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    Render(); double coldMs = (System.Diagnostics.Stopwatch.GetTimestamp() - coldStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    gc.Stop(); int coldEvents = gc.Count; yield return null;
                    if (i < 4) reports.Add(new { map, coldResourceFrame = i, cpuIncludingCameraMs = coldMs, gcEvents = coldEvents,
                        gpuNs = Recorder.Get("DN.Light.Total").gpuElapsedNanoseconds, freezeMs = field.DiffuseResources?.FreezeMilliseconds,
                        scope = "Fresh resource/cache; Shader compiler already warm" });
                }
                Save(map + "-continuous.png");
                settings.BounceMode = LightBounceMode.LegacyPoint; Render(); Save(map + "-legacy.png");
                settings.BounceMode = LightBounceMode.Continuous;
                reports.Add(new { map, mouth = new[] { mouth.x, mouth.y }, angle, geometryBuilds = field.DiffuseResources.BuildCount,
                    candidates = field.DiffuseResources.CandidateCount, bufferBytes = field.DiffuseResources.BufferBytes, workerMs = field.DiffuseResources.WorkerMilliseconds });
                if (map == "folded-expanded")
                {
                    foreach (int lights in new[] { 1, 2, 4, 16 })
                        foreach (string motion in dynamicOnly ? new[] { "dig", "region" } : new[] { "static", "rotate" })
                        {
                            var sequence = new[] { "A0", "B", "B", "A0", "A1", "B", "B", "A1", "Off", "B", "B", "Off" };
                            foreach (string mode in sequence)
                            {
                                File.WriteAllText(Path.Combine(output, "status.txt"), map + " " + lights + " " + motion + " " + mode);
                                settings.BounceMode = mode == "A0" ? LightBounceMode.LegacyPoint : mode == "Off" ? LightBounceMode.Off : LightBounceMode.Continuous;
                                field.DiffuseBenchmarkMode = mode == "A1" ? 1 : 0;
                                if (mode == "B" || mode == "A1")
                                {
                                    field.CountDiffuseQueries = true; Emit(lights, 0); Render(); yield return null;
                                    reports.Add(new { map, lights, motion, mode, queryCounts = field.DiffuseResources.ReadQueryCounts(), counterSample = true });
                                    field.CountDiffuseQueries = false;
                                }
                                for (int i = 0; i < 6; i++) { Motion(motion, i); Emit(lights, motion == "rotate" ? i * 1.5f : 0); (mode == "A0" ? baseline : field).Render(camera, preview, emitters); camera.Render(); yield return null; }
                                var warmed = UnityEngine.Rendering.AsyncGPUReadback.Request(Shader.GetGlobalTexture("_DNLightField"), 0);
                                while (!warmed.done) yield return null;
                                yield return null;
                                for (int i = 0; i < 12; i++)
                                {
                                    Motion(motion, i); Emit(lights, motion == "rotate" ? i * 1.5f : 0);
                                    foreach (string label in new[] { "DN.Light.Total", "DN.Diffuse.Profile", "DN.Diffuse.IntegrateCompose", "DN.Diffuse.Select" })
                                    { var recorder = Recorder.Get(label); recorder.enabled = false; recorder.enabled = true; }
                                    gc.Reset(); gc.Start(); long start = System.Diagnostics.Stopwatch.GetTimestamp();
                                    (mode == "A0" ? baseline : field).Render(camera, preview, emitters);
                                    double cpu = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                                    gc.Stop(); int events = gc.Count; long? bytes = events == 0 ? (long?)0 : null;
                                    camera.Render();
                                    var readback = UnityEngine.Rendering.AsyncGPUReadback.Request(Shader.GetGlobalTexture("_DNLightField"), 0);
                                    long gpuTime = 0, profileTime = 0, composeTime = 0, selectTime = 0; int gpuBlocks = 0;
                                    double collectUntil = EditorApplication.timeSinceStartup + 1.0;
                                    do
                                    {
                                        var counter = Recorder.Get("DN.Light.Total");
                                        if (counter.gpuSampleBlockCount > 0)
                                        {
                                            gpuTime = counter.gpuElapsedNanoseconds; gpuBlocks = counter.gpuSampleBlockCount;
                                            profileTime = Recorder.Get("DN.Diffuse.Profile").gpuElapsedNanoseconds;
                                            composeTime = Recorder.Get("DN.Diffuse.IntegrateCompose").gpuElapsedNanoseconds;
                                            selectTime = Recorder.Get("DN.Diffuse.Select").gpuElapsedNanoseconds;
                                        }
                                        yield return null;
                                    } while (!readback.done || (gpuTime == 0 && EditorApplication.timeSinceStartup < collectUntil));
                                    if (readback.hasError || gpuTime == 0) throw new InvalidOperationException("没有取得本次调度的有效 GPU 回执。");
                                    reports.Add(new { map, lights, motion, mode, cpuMs = cpu, gcBytes = bytes, gcEvents = events, gcMethod = "Unity GC.Alloc, validated positive control", gpuNs = gpuTime,
                                        gpuBlocks, profileNs = profileTime, composeNs = composeTime,
                                        selectNs = selectTime, processedPixels = 384 * 216,
                                        freezeMs = field.DiffuseResources?.FreezeMilliseconds ?? 0, uploadMs = field.DiffuseResources?.UploadMilliseconds ?? 0,
                                        prepareMs = field.DiffuseResources?.PrepareMilliseconds ?? 0, submitMs = field.SubmitMilliseconds,
                                        candidates = mode == "A0" || mode == "Off" ? 0 : field.DiffuseResources?.CandidateCount ?? 0 });
                                }
                            }
                        }
                }
                var retirement = field.RetireAsync(); while (!retirement.IsCompleted) yield return null;
                retirement.GetAwaiter().GetResult(); field = null; stage.Dispose();
                var oldRetirement = baseline.RetireAsync(); while (!oldRetirement.IsCompleted) yield return null;
                oldRetirement.GetAwaiter().GetResult(); baseline = null;
            }
        }

        private void Pose(MineMapDefinition map)
        {
            var room = map.Rooms.First(item => !item.Base);
            int centerX = map.OffsetX + room.X, centerY = map.OffsetY + room.Floor - 2;
            int x0 = Mathf.Clamp(centerX - 32, 0, 320 - 64), y0 = Mathf.Clamp(centerY - 32, 0, 192 - 64);
            var cells = new byte[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                cells[y * 64 + x] = (byte)(128 | (blueprint.MaterialAt(x0 + x, y0 + y) == 0 ? 0 : 64) | (int)blueprint.ShapeAt(x0 + x, y0 + y));
            var boundary = LightBoundaryBuilder.Build(new LightGeometrySnapshot(64, 64, cells), null);
            float best = float.MaxValue;
            for (int i = 0; i < boundary.Count; i++)
            {
                var edge = boundary[i]; var normal = new Vector2(edge.Nx, edge.Ny);
                var mid = new Vector2((edge.Ax + edge.Bx) * .5f + x0, (edge.Ay + edge.By) * .5f + y0);
                var candidate = mid + normal * 2;
                if (blueprint.MaterialAt((int)candidate.x, (int)candidate.y) != 0 || Vector2.Distance(new Vector2(edge.Ax, edge.Ay), new Vector2(edge.Bx, edge.By)) < 1) continue;
                float distance = Vector2.Distance(candidate, new Vector2(centerX, centerY));
                if (distance >= best) continue;
                best = distance; mouth = candidate; angle = Mathf.Atan2(normal.y, -normal.x) * Mathf.Rad2Deg;
            }
            if (best == float.MaxValue) throw new InvalidOperationException("代表区域没有可重放的受光墙段。");
        }
        private void Motion(string motion, int frame)
        {
            if (motion == "dig")
            {
                int x = Mathf.Clamp(Mathf.FloorToInt(mouth.x + Mathf.Cos(angle * Mathf.Deg2Rad) * 2.1f), 0, 319);
                int y = Mathf.Clamp(Mathf.FloorToInt(mouth.y - Mathf.Sin(angle * Mathf.Deg2Rad) * 2.1f), 0, 191);
                byte original = blueprint.MaterialAt(x, y), shape = (byte)blueprint.ShapeAt(x, y);
                if (original == 0) throw new InvalidOperationException("挖墙性能姿态没有命中真实岩体。");
                stage.Source.ApplyChanges(new[] { new TerrainBlueprintCellChange(x, y, frame % 2 == 0 ? (byte)0 : original, frame % 2 == 0 ? (byte)0 : shape) });
                preview.TickFromEditor();
            }
            else if (motion == "region")
            {
                camera.transform.position = new Vector3(mouth.x - .5f + (frame % 2 == 0 ? 0 : 8), -mouth.y + .5f, -10);
                preview.TickFromEditor();
            }
        }
        private void Emit(int count, float turn)
        {
            emitters.Clear();
            var rules = new LightEmissionRules(14, 90, 1.35f, 2.2f, 0, 1, .9f, .72f, .375f);
            for (int i = 0; i < count; i++) emitters.Add(new LightEmitterData(new Vector3(mouth.x - .5f, -mouth.y + .5f, 0), angle + turn + i * 2, rules));
        }
        private void Render() { preview.TickFromEditor(); field.Render(camera, preview, emitters); camera.Render(); }
        private void Save(string name)
        {
            var image = new Texture2D(stage.Image.width, stage.Image.height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try { RenderTexture.active = (RenderTexture)stage.Image; image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
        }
        private static T Read<T>(object target, string member) => (T)target.GetType().GetField(member, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
