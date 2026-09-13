using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Diagnostics;
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
        private readonly MeasurementSeries reports = new MeasurementSeries();
        private readonly MeasurementSeries foregroundFrames = new MeasurementSeries();
        private readonly MeasurementSeries foregroundAllocations = new MeasurementSeries();
        private ProfilerRecorder gc;
        private double started = -1, last;
        private long receivedBytes, receivedFrames;
        private long focusedFrames;
        private double nextMemoryAt;
        private bool wasForeground;
        private readonly List<long[]> memory = new List<long[]>();

        public void Initialize(SessionNetwork value)
        {
            network = value;
            network.Client.EnableMeasurements();
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            network.Client.Updated += Updated;
        }

        private void Updated(DarkNights.Core.ViewData.SessionViewData frame)
        {
            receivedFrames++;
            receivedBytes += network.Client.LastPayloadBytes;
        }

        public void ResetWindow()
        {
            frames.Clear(); allocations.Clear(); reports.Clear(); memory.Clear();
            foregroundFrames.Clear(); foregroundAllocations.Clear(); wasForeground = false;
            network.Client.Measurements.Clear();
            network.Server?.Measurements?.Clear();
            receivedBytes = receivedFrames = focusedFrames = 0;
            nextMemoryAt = last = 0;
            started = -1;
        }

        public void RecordReport(double milliseconds)
        {
            if (started >= 0 && Time.realtimeSinceStartupAsDouble - started > 2) reports.Add(milliseconds);
        }

        private void Update()
        {
            if (!network.Client.Ready) return;
            double now = Time.realtimeSinceStartupAsDouble;
            bool foreground = IsOsForeground();
            if (started < 0) { started = last = now; return; }
            if (now - started > 2)
            {
                frames.Add((now - last) * 1000);
                if (Application.isFocused) focusedFrames++;
                if (gc.Valid) allocations.Add(gc.LastValue);
                if (foreground && wasForeground)
                {
                    foregroundFrames.Add((now - last) * 1000);
                    if (gc.Valid) foregroundAllocations.Add(gc.LastValue);
                }
            }
            last = now;
            wasForeground = foreground;
            if (now >= nextMemoryAt && memory.Count < 1024)
            {
                nextMemoryAt = now + 5;
                memory.Add(new[] { (long)((now - started) * 1000), GC.GetTotalMemory(false) });
            }
        }

        public void Save(string path)
        {
            var authority = network.Server?.Measurements;
            var frame = network.Client.Replica.Current;
            double seconds = Math.Max(.001, Time.realtimeSinceStartupAsDouble - started);
            object Summary(MeasurementSeries value) => value == null ? null : new
            {
                samples = value.Count, retainedSamples = value.RetainedCount,
                mean = value.Count == 0 ? 0 : value.Total / value.Count,
                p95 = value.Percentile(.95), p99 = value.Percentile(.99), maximum = value.Maximum
            };
            File.WriteAllText(path, JsonConvert.SerializeObject(new
            {
                utc = DateTime.UtcNow.ToString("O"), seconds, frameMilliseconds = Summary(frames),
                quantileMethod = MeasurementSeries.QuantileMethod,
                foregroundFrameMilliseconds = Summary(foregroundFrames),
                foregroundFrameAllocatedBytes = Summary(foregroundAllocations),
                foregroundSource = "Windows GetForegroundWindow / GetWindowThreadProcessId; both interval endpoints must belong to this process",
                frameAllocatedBytes = Summary(allocations), gcRecorderValid = gc.Valid,
                automationReportMilliseconds = Summary(reports),
                clientDecodeMilliseconds = Summary(network.Client.Measurements.DecodeMilliseconds),
                clientObjectsMilliseconds = Summary(network.Client.Measurements.ObjectsMilliseconds),
                clientApplyMilliseconds = Summary(network.Client.Measurements.ApplyMilliseconds),
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
                actorCount = frame?.World.Actors.Count, entityCount = frame?.World.Identities.Count, projectileCount = frame?.World.Projectiles.Count,
                unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), processor = SystemInfo.processorType,
                memoryMb = SystemInfo.systemMemorySize,
                screenWidth = Screen.width, screenHeight = Screen.height, Application.runInBackground,
                Application.isBatchMode, focusedFrames,
                memorySamples = memory, memorySampleColumns = new[] { "elapsedMilliseconds", "managedUsedBytes" },
                workingSetSource = "External Windows process sampling in acceptance script; Mono Process.WorkingSet64 is not used.",
                caveat = "Rendered automation workload, including background frames. Full-history quantiles are histogram upper bounds within 1%; means use all samples. UDP and OS working set are measured separately. Unsupported per-thread counters are null."
            }, Formatting.Indented));
        }

        private void OnDestroy()
        {
            gc.Dispose();
            if (network != null) network.Client.Updated -= Updated;
        }

        private static bool IsOsForeground()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            IntPtr window = GetForegroundWindow();
            GetWindowThreadProcessId(window, out uint process);
            return window != IntPtr.Zero && process == GetCurrentProcessId();
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
#endif
    }
}
