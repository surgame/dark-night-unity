using System;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Networking;
using AnyRules.Next.Unity;

namespace DarkNights.View.Terrain
{
    /// <summary>同世界前景的局部装卸；流换代后卸载旧逻辑块和页面，用完整新区域重建输入，保留宿主背景、光纹理与作者资源。</summary>
    internal sealed class TerrainReplicaRegion
    {
        private readonly ARDMapController controller;
        private readonly TerrainReplicaSource source;
        private readonly Action hide;
        private readonly CancellationToken cancellation;
        private ulong session, generation;
        public GridBounds Loaded { get; private set; }
        public bool Loading { get; private set; }
        public Exception LastError { get; private set; }
        internal TerrainReplicaRegion(ARDMapController controller, TerrainReplicaSource source,
            Action hide, CancellationToken cancellation)
        { this.controller = controller; this.source = source; this.hide = hide; this.cancellation = cancellation; }
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
                hide(); source.ForgetLoadedRegion();
                await controller.UnloadRegionAsync(controller.Descriptor.Bounds, MapUnloadPolicy.DiscardUnsaved, cancellation);
                await controller.LoadRegionAsync(region, cancellation);
                Loaded = region; session = replica.Session; generation = replica.Generation;
                source.PublishInitialBaseline();
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { LastError = error; }
            finally { Loading = false; }
        }
    }
}
