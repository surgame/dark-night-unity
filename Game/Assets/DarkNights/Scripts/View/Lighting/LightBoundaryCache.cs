using System;
using System.Threading;
using System.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.View.Terrain;
using UnityEngine;
using UnityEngine.Profiling;

namespace DarkNights.View.Lighting
{
    /// <summary>局部边界后台任务和主线程安装票据；源、代次、区域和几何版本必须同时有效，纯灯束运动不重新捕获地图。</summary>
    public sealed class LightBoundaryCache
    {
        private readonly Func<LightGeometrySnapshot, Func<bool>, LightBoundarySnapshot> build;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private Task<LightBoundarySnapshot> pending;
        private CaveVisualSource requested, installed;
        private RectInt requestedBounds;
        private long requestedRevision, installedRevision = -1, generation, requestedGeneration;
        private bool retired;
        public LightBoundarySnapshot Snapshot { get; private set; }
        public RectInt Bounds { get; private set; }
        public int BuildCount { get; private set; }
        public int DiscardCount { get; private set; }
        public double WorkerMilliseconds { get; private set; }
        public double FreezeMilliseconds { get; private set; }

        public LightBoundaryCache(Func<LightGeometrySnapshot, Func<bool>, LightBoundarySnapshot> builder)
        { build = builder ?? throw new ArgumentNullException(nameof(builder)); }

        public void Tick(CaveVisualSource source, RectInt bounds)
        {
            FreezeMilliseconds = 0;
            if (retired) return;
            if (source == null || source.IsRetired) { Snapshot = null; installed = null; generation++; return; }
            if (Snapshot != null && installed != source)
            { Snapshot = null; installed = null; generation++; }
            if (pending != null && pending.IsCompleted)
            {
                try
                {
                    var result = pending.GetAwaiter().GetResult();
                    if (!source.IsRetired && requested == source && requestedRevision == source.GeometryRevision && requestedBounds == bounds && requestedGeneration == generation)
                    { Snapshot = result; installed = source; installedRevision = requestedRevision; Bounds = bounds; }
                    else DiscardCount++;
                }
                catch (OperationCanceledException) { DiscardCount++; }
                finally { pending = null; }
            }
            if (pending != null || source == null || (Snapshot != null && installedRevision == source.GeometryRevision && Bounds == bounds)) return;
            Schedule(source, bounds);
        }

        private void Schedule(CaveVisualSource source, RectInt bounds)
        {
            Profiler.BeginSample("DN.Diffuse.Freeze");
            LightGeometrySnapshot frozen;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            try { frozen = source.CaptureLightGeometry(bounds); }
            finally { Profiler.EndSample(); }
            FreezeMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            requested = source; requestedBounds = bounds; requestedRevision = source.GeometryRevision;
            requestedGeneration = generation; BuildCount++;
            var token = lifetime.Token;
            pending = Task.Run(() =>
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                try { return build(frozen, () => token.IsCancellationRequested); }
                finally { WorkerMilliseconds = clock.Elapsed.TotalMilliseconds; }
            }, token);
        }

        public async Task RetireAsync()
        {
            if (retired) return;
            retired = true; generation++; Snapshot = null; installed = null; lifetime.Cancel();
            try { if (pending != null) await pending; }
            catch (OperationCanceledException) { }
            finally { pending = null; lifetime.Dispose(); }
        }
    }
}
