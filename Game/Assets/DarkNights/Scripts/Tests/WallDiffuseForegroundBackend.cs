using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DarkNights.View.Lighting;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace DarkNights.Tests
{
    /// <summary>前台 Bootstrap 的临时照明计量适配；复用真实相机、地形和作者灯具规则，仅复制展示光源作一／四灯负载，退出恢复原后端，不创建业务对象。</summary>
    public sealed class WallDiffuseForegroundBackend : IEnvironmentLightBackend, IDisposable
    {
        private readonly ExplorationLightField candidate;
        private readonly WallDiffuseMeasuredReferenceField reference;
        private readonly List<LightEmitterData> lights = new List<LightEmitterData>(16);
        private readonly List<double> cpu = new List<double>(512);
        private readonly List<long> gc = new List<long>(512);
        private readonly ProfilerRecorder allocations;
        private ProfilerRecorder gpu;
        private bool running, optimized, wasProfiling;
        private string motion, receipt;
        private int count, frame, foreground, missingLight;
        private Core.Config.LightEmissionRules observedRules;
        public ExplorationLightSettings Settings => candidate.Settings;
        public int SourceCount => optimized ? candidate.SourceCount : reference.SourceCount;

        public WallDiffuseForegroundBackend(ExplorationLightField candidate, WallDiffuseMeasuredReferenceField reference)
        {
            this.candidate = candidate; this.reference = reference;
            wasProfiling = Profiler.enabled; Profiler.enabled = true;
            allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1024,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.StartImmediately);
            var control = new byte[8192]; GC.KeepAlive(control); allocations.Stop();
            if (!allocations.Valid || allocations.Count == 0) throw new InvalidOperationException("前台 Render GC 自检失败。");
        }

        public void Select(int count, string motion, bool optimized)
        { this.count = count; this.motion = motion; this.optimized = optimized; frame = 0; }

        public void Begin()
        {
            cpu.Clear(); gc.Clear(); foreground = missingLight = 0;
            receipt = "DN.Foreground." + Guid.NewGuid().ToString("N");
            var marker = CustomSampler.Create(receipt, true);
            gpu = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, receipt, 1024,
                ProfilerRecorderOptions.GpuRecorder | ProfilerRecorderOptions.SumAllSamplesInFrame);
            if (!gpu.Valid) throw new InvalidOperationException("前台 GPU 计量不可用。");
            candidate.MeasurementSampler = optimized ? marker : null;
            reference.MeasurementSampler = optimized ? null : marker; running = true;
        }

        public void Pause()
        { running = false; candidate.MeasurementSampler = null; reference.MeasurementSampler = null; }

        public object End()
        {
            gpu.Stop(); var samples = new List<ProfilerRecorderSample>(); gpu.CopyTo(samples); gpu.Dispose();
            var values = new List<double>(); int duplicates = 0;
            foreach (var sample in samples)
            {
                if (sample.Count != 1) { duplicates++; continue; }
                if (sample.Value > 0) values.Add(sample.Value / 1000000.0);
            }
            int missing = Math.Max(0, cpu.Count - values.Count);
            return new { backend = optimized ? "optimized" : "reference", lights = count, motion, receipt,
                complete = cpu.Count >= 30 && values.Count >= 30 && foreground == cpu.Count && missingLight == 0 && duplicates == 0,
                renderSamples = cpu.Count, foregroundSamples = foreground, missingLight, missingGpu = missing, duplicateGpu = duplicates,
                cpuMs = cpu.ToArray(), gpuMs = values.ToArray(), gcBytes = gc.ToArray(), rules = observedRules,
                scope = "Actual Bootstrap Render CPU and lighting GPU batch; whole game frame timings are reported separately. GPU windows are aggregate receipts, not per-frame CPU/GPU pairs." };
        }

        public void Render(Camera camera, View.Terrain.TerrainPreview preview, IReadOnlyList<LightEmitterData> source)
        {
            lights.Clear(); LightEmitterData first = default; bool found = false;
            for (int i = 0; i < source.Count; i++) if (source[i].Directional) { first = source[i]; found = true; break; }
            if (!found) { if (running) missingLight++; lights.AddRange(source); }
            else
            {
                observedRules = first.Rules;
                for (int i = 0; i < count; i++)
                {
                    Vector3 shift = motion == "move" ? Vector3.right * (Mathf.Sin(frame * .04f) * .25f) : Vector3.zero;
                    float turn = motion == "rotate" ? Mathf.Sin(frame * .04f) * 12 : 0;
                    lights.Add(new LightEmitterData(first.Position + shift, first.Angle + turn + i * 2, first.Rules, true, first.NearPosition + shift));
                }
                for (int i = 0; i < source.Count; i++) if (!source[i].Directional) lights.Add(source[i]);
            }
            frame++; var backend = optimized ? (IEnvironmentLightBackend)candidate : reference;
            if (!running) { backend.Render(camera, preview, lights); return; }
            if (cpu.Count >= 512) throw new InvalidOperationException("前台采样超过有限容量。");
            allocations.Reset(); allocations.Start(); long begin = System.Diagnostics.Stopwatch.GetTimestamp();
            try { backend.Render(camera, preview, lights); }
            finally { allocations.Stop(); }
            double elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - begin) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            long bytes = 0; for (int i = 0; i < allocations.Count; i++) bytes += allocations.GetSample(i).Value;
            cpu.Add(elapsed); gc.Add(bytes); if (IsForeground()) foreground++;
        }

        public static bool IsForeground()
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out uint process);
            return process == GetCurrentProcessId();
        }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
        public void Suspend() => ExplorationLightField.Suspend();
        public Task RetireAsync() => Task.WhenAll(candidate.RetireAsync(), reference.RetireAsync());
        public void Dispose() { Pause(); if (gpu.Valid) gpu.Dispose(); allocations.Dispose(); Profiler.enabled = wasProfiling; }
    }
}
