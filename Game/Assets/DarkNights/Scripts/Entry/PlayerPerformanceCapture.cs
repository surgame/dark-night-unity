using System;
using System.IO;
using System.Collections.Generic;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using Newtonsoft.Json;
using Unity.Profiling;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>
    /// 显式验收参数启用的 Player 性能记录器，采样真实帧间隔、GC 分配及权威步时，导出一次冻结摘要。
    /// 自动化报告与操作本身的开销包含在 Player 指标内；不修改帧率、规则倍速或产品画面设置。
    /// </summary>
    public sealed class PlayerPerformanceCapture : MonoBehaviour
    {
        private SessionNetwork network;
        private readonly MeasurementSeries frames = new MeasurementSeries();
        private readonly MeasurementSeries allocations = new MeasurementSeries();
        private ProfilerRecorder gc;
        private double started = -1, last;
        private long receivedBytes, receivedFrames;
        private double nextMemoryAt;
        private readonly List<long[]> memory = new List<long[]>();

        public void Initialize(SessionNetwork value)
        {
            network = value;
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            network.Client.Updated += Updated;
        }

        private void Updated(DarkNights.Core.ViewData.SessionViewData frame)
        {
            receivedFrames++;
            receivedBytes += network.Client.LastPayloadBytes;
        }

        private void Update()
        {
            if (!network.Client.Ready) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (started < 0) { started = last = now; return; }
            if (now - started > 2)
            {
                frames.Add((now - last) * 1000);
                if (gc.Valid) allocations.Add(gc.LastValue);
            }
            last = now;
            if (now >= nextMemoryAt && memory.Count < 1024)
            {
                nextMemoryAt = now + 5;
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                    memory.Add(new[] { (long)((now - started) * 1000), GC.GetTotalMemory(false), process.WorkingSet64 });
            }
        }

        public void Save(string path)
        {
            var authority = network.Server?.Measurements;
            var frame = network.Client.Replica.Current;
            double seconds = Math.Max(.001, Time.realtimeSinceStartupAsDouble - started);
            object Summary(MeasurementSeries value) => value == null ? null : new
            {
                samples = value.Count, mean = value.Count == 0 ? 0 : value.Total / value.Count,
                p95 = value.Percentile(.95), p99 = value.Percentile(.99), maximum = value.Maximum
            };
            File.WriteAllText(path, JsonConvert.SerializeObject(new
            {
                utc = DateTime.UtcNow.ToString("O"), seconds, frameMilliseconds = Summary(frames),
                frameAllocatedBytes = Summary(allocations), gcRecorderValid = gc.Valid,
                authorityStepMilliseconds = Summary(authority?.StepMilliseconds),
                threadAllocationCounterValid = authority?.ThreadAllocationCounterValid ?? false,
                authorityStepAllocatedBytes = Summary(authority?.ThreadAllocationCounterValid == true ? authority.StepAllocatedBytes : null),
                projectionMilliseconds = Summary(authority?.ProjectionMilliseconds),
                projectionAllocatedBytes = Summary(authority?.ThreadAllocationCounterValid == true ? authority.ProjectionAllocatedBytes : null),
                applicationPayloadBytesToRemotePeers = authority?.PayloadBytes ?? 0,
                applicationPayloadBytesPerSecond = (authority?.PayloadBytes ?? 0) / seconds,
                maximumBacklogSeconds = authority?.MaximumBacklogSeconds ?? 0,
                receivedFrames, receivedBytes, receivedBytesPerSecond = receivedBytes / seconds,
                publication = frame?.Publication, serverTick = frame?.ServerTick, epoch = frame?.Epoch,
                actorCount = frame?.World.Actors.Count, projectileCount = frame?.World.Projectiles.Count,
                unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), processor = SystemInfo.processorType,
                memoryMb = SystemInfo.systemMemorySize,
                memorySamples = memory, memorySampleColumns = new[] { "elapsedMilliseconds", "managedUsedBytes", "workingSetBytes" },
                caveat = "Frame metrics include automation overhead; UDP bytes measured separately. Unsupported per-thread counters are null, not zero allocation."
            }, Formatting.Indented));
        }

        private void OnDestroy()
        {
            gc.Dispose();
            if (network != null) network.Client.Updated -= Updated;
        }
    }
}
