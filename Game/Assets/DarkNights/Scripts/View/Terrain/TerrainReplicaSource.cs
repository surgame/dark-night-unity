using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Networking;
using AnyRules.Next.Unity;

namespace DarkNights.View.Terrain
{
    /// <summary>网络原子提交到冻结本地输入的适配器；在通知内捕获值，宿主退出通知栈后安装。</summary>
    public sealed class TerrainReplicaSource : ITerrainInputSource
    {
        private readonly IReadOnlyGrid source;
        private readonly Dictionary<ChunkCoord, ulong> fingerprints = new Dictionary<ChunkCoord, ulong>();
        private WorldDescriptor descriptor;
        private ulong inputGeneration = 1, lastSourceCommit, sourceSession, streamGeneration;
        private bool streamIdentityKnown, requiresFreshBaseline;
        public event Action<MapInputBatch> InputChanged;

        public TerrainReplicaSource(IReadOnlyGrid source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            if (source is ChunkReplicaStateMachine state)
            {
                descriptor = state.Descriptor; sourceSession = state.Session; streamGeneration = state.Generation;
                lastSourceCommit = state.CommitId; streamIdentityKnown = descriptor != null;
            }
        }

        public void PublishInitialBaseline()
        {
            if (!TryPublishFreshBaseline(source.CommitId))
                throw new InvalidOperationException("完整网络基线尚未就绪；Unknown 不能变为空格。");
        }

        public Task<MapChunkData> LoadAsync(WorldDescriptor world, ChunkCoord coordinate, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (descriptor == null) descriptor = world;
            else if (!descriptor.World.Equals(world.World)) throw new InvalidOperationException("网络表现源不能跨世界复用。");
            var snapshot = CaptureSnapshot(coordinate);
            fingerprints[coordinate] = Fingerprint(snapshot.Cells, coordinate, world);
            var cells = new GridCell[snapshot.Cells.Count];
            for (int i = 0; i < cells.Length; i++) cells[i] = snapshot.Cells[i];
            return Task.FromResult(new MapChunkData(coordinate, cells, readOnly: true));
        }

        /// <summary>显式诊断修复使用完整基线；同一个源提交也可重新建立表现，不伪造递增的网络提交号。</summary>
        public void NotifyChanged()
        {
            if (descriptor == null || fingerprints.Count == 0) return;
            PublishInitialBaseline();
        }

        public void NotifyChanged(MapReplicaChange transition)
        {
            if (transition == null) throw new ArgumentNullException(nameof(transition));
            if (transition.Kind == MapReplicaChangeKind.WorldReset || transition.Kind == MapReplicaChangeKind.VisibilityRevoked ||
                transition.Kind == MapReplicaChangeKind.Disconnected)
            {
                inputGeneration = checked(inputGeneration + 1); sourceSession = transition.Session;
                streamGeneration = transition.StreamGeneration; streamIdentityKnown = true;
                lastSourceCommit = 0; requiresFreshBaseline = true;
                var targetWorld = descriptor == null ? transition.World : descriptor.World;
                var kind = transition.Kind == MapReplicaChangeKind.WorldReset ? MapInputBatchKind.Reset :
                    transition.Kind == MapReplicaChangeKind.VisibilityRevoked ? MapInputBatchKind.VisibilityRevoked : MapInputBatchKind.Disconnected;
                InputChanged?.Invoke(new MapInputBatch(targetWorld, inputGeneration, sourceSession, streamGeneration, 0, kind));
                return;
            }
            if (descriptor == null || !transition.World.Equals(descriptor.World))
                throw new InvalidOperationException("世界变化必须更换地形表现宿主。");
            bool streamChanged = streamIdentityKnown &&
                (sourceSession != transition.Session || streamGeneration != transition.StreamGeneration);
            if (streamChanged)
            {
                inputGeneration = checked(inputGeneration + 1); lastSourceCommit = 0; requiresFreshBaseline = true;
                InputChanged?.Invoke(new MapInputBatch(descriptor.World, inputGeneration, transition.Session,
                    transition.StreamGeneration, 0, MapInputBatchKind.Reset));
            }
            sourceSession = transition.Session; streamGeneration = transition.StreamGeneration; streamIdentityKnown = true;
            if (requiresFreshBaseline) { TryPublishFreshBaseline(transition.Commit); return; }
            if (transition.Commit <= lastSourceCommit) return;
            var cells = new List<MapInputCell>(transition.Cells.Count);
            foreach (var position in transition.Cells)
            {
                if (!descriptor.Bounds.Contains(position)) continue;
                if (!source.Read(position).TryGetCell(out var value)) { RequestCompleteBaseline(transition.Commit); return; }
                cells.Add(new MapInputCell(position, value));
            }
            var snapshots = new List<MapInputChunk>(transition.SnapshotChunks.Count);
            foreach (var coordinate in transition.SnapshotChunks) snapshots.Add(CaptureSnapshot(coordinate));
            MapInputBatchKind batchKind = snapshots.Count == 0 ? MapInputBatchKind.Delta :
                IsCompleteSnapshotSet(snapshots) ? MapInputBatchKind.Baseline : MapInputBatchKind.SnapshotUpdate;
            var batch = new MapInputBatch(transition.World, inputGeneration, sourceSession, streamGeneration,
                transition.Commit, batchKind, cells, snapshots);
            lastSourceCommit = transition.Commit;
            InputChanged?.Invoke(batch);
        }

        private bool TryPublishFreshBaseline(ulong sourceCommit)
        {
            if (descriptor == null || fingerprints.Count == 0) return false;
            var coordinates = new List<ChunkCoord>(fingerprints.Keys); coordinates.Sort();
            var snapshots = new List<MapInputChunk>(coordinates.Count);
            foreach (var coordinate in coordinates)
            {
                if (!TryCaptureSnapshot(coordinate, out var snapshot)) return false;
                snapshots.Add(snapshot);
            }
            foreach (var snapshot in snapshots)
                fingerprints[snapshot.Coordinate] = Fingerprint(snapshot.Cells, snapshot.Coordinate, descriptor);
            lastSourceCommit = sourceCommit; requiresFreshBaseline = false;
            InputChanged?.Invoke(new MapInputBatch(descriptor.World, inputGeneration, sourceSession, streamGeneration,
                sourceCommit, MapInputBatchKind.Baseline, snapshotChunks: snapshots));
            return true;
        }

        private void RequestCompleteBaseline(ulong sourceCommit)
        {
            if (!requiresFreshBaseline)
            {
                inputGeneration = checked(inputGeneration + 1); lastSourceCommit = 0; requiresFreshBaseline = true;
                InputChanged?.Invoke(new MapInputBatch(descriptor.World, inputGeneration, sourceSession, streamGeneration,
                    0, MapInputBatchKind.Reset));
            }
            TryPublishFreshBaseline(sourceCommit);
        }

        private bool TryCaptureSnapshot(ChunkCoord coordinate, out MapInputChunk snapshot)
        {
            int size = descriptor.ChunkSize; var cells = new GridCell[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var position = new CellCoord(checked(coordinate.U * size + x), checked(coordinate.V * size + y));
                if (!descriptor.Bounds.Contains(position)) continue;
                if (!source.Read(position).TryGetCell(out cells[y * size + x])) { snapshot = null; return false; }
            }
            snapshot = new MapInputChunk(coordinate, cells); return true;
        }

        private bool IsCompleteSnapshotSet(IReadOnlyList<MapInputChunk> snapshots)
        {
            if (fingerprints.Count == 0 || snapshots.Count != fingerprints.Count) return false;
            var received = new HashSet<ChunkCoord>();
            foreach (var snapshot in snapshots) received.Add(snapshot.Coordinate);
            foreach (var coordinate in fingerprints.Keys) if (!received.Contains(coordinate)) return false;
            return received.Count == fingerprints.Count;
        }
        private MapInputChunk CaptureSnapshot(ChunkCoord coordinate)
        {
            if (TryCaptureSnapshot(coordinate, out var snapshot)) return snapshot;
            throw new InvalidOperationException("网络快照含 Unknown，不能按空地安装。");
        }
        private static ulong Fingerprint(IReadOnlyList<GridCell> cells, ChunkCoord coordinate, WorldDescriptor world)
        {
            const ulong offset = 1469598103934665603UL, prime = 1099511628211UL;
            ulong result = offset; int size = world.ChunkSize;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var position = new CellCoord(checked(coordinate.U * size + x), checked(coordinate.V * size + y));
                bool known = world.Bounds.Contains(position); result ^= known ? 1UL : 0UL; result *= prime;
                if (!known) continue;
                var cell = cells[y * size + x]; result ^= cell.TileId; result *= prime;
                result ^= unchecked((ulong)(ushort)cell.Height); result *= prime; result ^= cell.Flags; result *= prime;
            }
            return result;
        }
    }
}
