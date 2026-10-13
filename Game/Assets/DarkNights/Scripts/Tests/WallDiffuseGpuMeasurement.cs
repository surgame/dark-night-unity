using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace DarkNights.Tests
{
    /// <summary>单次照明调度的 CPU／GPU／GC 计量；每次使用唯一 GPU 标记和完成屏障拒绝迟到回执，GC 先用已知分配自检，计时不包含回读与报告写入。</summary>
    public sealed class WallDiffuseGpuMeasurement : IDisposable
    {
        private readonly ProfilerRecorder allocations;
        private readonly bool wasProfiling;
        private int sequence;
        private readonly string batch = Guid.NewGuid().ToString("N");
        private readonly CustomSampler[] samplers = new CustomSampler[4096];
        public double CpuMilliseconds { get; private set; }
        public long GpuNanoseconds { get; private set; }
        public long GcBytes { get; private set; }
        public int GcEvents { get; private set; }
        public string Receipt { get; private set; }
        public string MissingGpuReason { get; private set; }

        public WallDiffuseGpuMeasurement()
        {
            wasProfiling = Profiler.enabled; Profiler.enabled = true;
            // 标记必须在地图预热之前注册；临近调度动态创建会丢失轻负载的 GPU 回执。
            for (int i = 0; i < samplers.Length; i++) samplers[i] = CustomSampler.Create("DN.Pruning." + batch + "." + (i + 1), true);
            allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1024,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.StartImmediately);
            var control = new byte[8192]; GC.KeepAlive(control); allocations.Stop();
            if (!allocations.Valid || allocations.Count == 0) throw new InvalidOperationException("GC.Alloc 正向自检未通过。");
        }

        public IEnumerator Run(Action render, Action<CustomSampler> setMarker)
        {
            Receipt = "DN.Pruning." + batch + "." + (++sequence);
            if (sequence > samplers.Length) throw new InvalidOperationException("有限 GPU 标记容量耗尽。");
            var sampler = samplers[sequence - 1]; MissingGpuReason = null;
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, Receipt, 32,
                ProfilerRecorderOptions.GpuRecorder | ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            if (!recorder.Valid) throw new InvalidOperationException("GPU 专用记录器不可用。");
            // GPU Recorder 在帧边界生效；不能在创建记录器的同一 Editor 回调内立即提交短命令批次。
            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;
            var gpuSamples = new List<ProfilerRecorderSample>(32);
            setMarker(sampler);
            try
            {
                allocations.Reset(); allocations.Start(); long begin = System.Diagnostics.Stopwatch.GetTimestamp();
                render();
                CpuMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - begin) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            }
            finally { allocations.Stop(); setMarker(null); }
            GcEvents = allocations.Count; GcBytes = 0;
            if (GcEvents >= 1024) throw new InvalidOperationException("GC 回执超过容量，不签署本样本。");
            for (int i = 0; i < GcEvents; i++) GcBytes += allocations.GetSample(i).Value;
            bool completed = false, readbackError = false;
            // 回读结果仅在完成当帧有效；GPU Recorder 延迟数帧，必须在回调中冻结完成状态。
            AsyncGPUReadback.Request(Shader.GetGlobalTexture("_DNLightField"), 0, request => { readbackError = request.hasError; completed = true; });
            double deadline = EditorApplication.timeSinceStartup + 2;
            GpuNanoseconds = 0;
            do
            {
                int received = 0;
                gpuSamples.Clear(); recorder.CopyTo(gpuSamples);
                foreach (var sample in gpuSamples)
                {
                    if (sample.Value <= 0) continue;
                    if (sample.Count != 1) throw new InvalidOperationException("GPU 样本包含多次调度：" + Receipt);
                    GpuNanoseconds = sample.Value; received++;
                }
                if (received > 1) throw new InvalidOperationException("唯一 GPU 标记返回重复样本：" + Receipt);
                EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
                if (EditorApplication.timeSinceStartup > deadline)
                {
                    if (!completed) throw new TimeoutException("GPU 工作未完成：" + Receipt);
                    MissingGpuReason = "GPU completed; unique timing receipt missing; excluded from GPU comparison";
                    break;
                }
                yield return null;
            } while (!completed || GpuNanoseconds <= 0);
            if (readbackError) throw new InvalidOperationException("GPU 回读失败：" + Receipt);
        }

        public void Dispose()
        {
            allocations.Dispose(); Profiler.enabled = wasProfiling;
        }
    }
}
