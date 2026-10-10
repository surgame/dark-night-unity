using System;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Networking;
using AnyRules.Next.Unity;

namespace DarkNights.View.Terrain
{
    /// <summary>同世界前景和矿层共用的局部装卸；先装入完整目标区再退休退出区块，保留重叠页面，输入身份由完整基线切换。</summary>
    internal sealed class TerrainReplicaRegion
    {
        private readonly ARDMapController controller;
        private readonly TerrainReplicaSource source;
        private readonly CancellationToken cancellation;
        private ulong session, generation;
        public GridBounds Loaded { get; private set; }
        public bool Loading { get; private set; }
        public Exception LastError { get; private set; }
        internal TerrainReplicaRegion(ARDMapController controller, TerrainReplicaSource source, CancellationToken cancellation)
        { this.controller = controller; this.source = source; this.cancellation = cancellation; }
        internal async Task Initialize(GridBounds region, ChunkReplicaStateMachine replica)
        {
            await controller.LoadRegionAsync(region, cancellation);
            Loaded = region; session = replica?.Session ?? 0; generation = replica?.Generation ?? 0;
        }
        internal void Present(ChunkReplicaStateMachine replica, GridBounds region, bool ready)
        {
            if (Loading || !ready || replica?.Descriptor == null || cancellation.IsCancellationRequested || LastError != null) return;
            if (session == replica.Session && generation == replica.Generation && region.Equals(Loaded)) return;
            _ = Replace(replica, region);
        }
        private async Task Replace(ChunkReplicaStateMachine replica, GridBounds region)
        {
            Loading = true;
            try
            {
                source.PrepareRegion(region);
                await controller.LoadRegionAsync(region, cancellation);
                await UnloadOutside(region);
                Loaded = region; session = replica.Session; generation = replica.Generation;
                source.PublishInitialBaseline();
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { LastError = error; }
            finally { Loading = false; }
        }
        private async Task UnloadOutside(GridBounds region)
        {
            int size = controller.Descriptor.ChunkSize;
            for (int v = GridMath.FloorDiv(Loaded.MinV, size); v <= GridMath.FloorDiv((int)Loaded.MaxVExclusive - 1, size); v++)
                for (int u = GridMath.FloorDiv(Loaded.MinU, size); u <= GridMath.FloorDiv((int)Loaded.MaxUExclusive - 1, size); u++)
                {
                    int left = u * size, bottom = v * size;
                    if (left < region.MaxUExclusive && left + size > region.MinU &&
                        bottom < region.MaxVExclusive && bottom + size > region.MinV) continue;
                    var bounds = controller.Descriptor.Bounds;
                    int x = Math.Max(left, bounds.MinU), y = Math.Max(bottom, bounds.MinV);
                    int right = (int)Math.Min(left + size, bounds.MaxUExclusive), top = (int)Math.Min(bottom + size, bounds.MaxVExclusive);
                    await controller.UnloadRegionAsync(new GridBounds(x, y, right - x, top - y), MapUnloadPolicy.DiscardUnsaved, cancellation);
                }
        }
    }
}
