using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Networking;
using AnyRules.Next.Unity;

namespace DarkNights.View.Terrain
{
    /// <summary>把网络只读副本的原子提交冻结为本地源输入；增量只捕获提交格，基线显式携带完整区块。</summary>
    public sealed class TerrainReplicaSource : ITerrainInputSource
    {
        private readonly IReadOnlyGrid source;
        private readonly Dictionary<ChunkCoord, ulong> fingerprints = new Dictionary<ChunkCoord, ulong>();
        private WorldDescriptor descriptor;
        private ulong inputGeneration = 1, lastSourceCommit;
        private ulong sourceSession, streamGeneration;
        private bool streamIdentityKnown;
        private bool requiresFreshBaseline;
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
            if (descriptor == null || fingerprints.Count == 0) throw new InvalidOperationException("网络地图区块尚未完成初始装载。");
            var coordinates = new List<ChunkCoord>(fingerprints.Keys); coordinates.Sort();
            var chunks = new List<MapInputChunk>(coordinates.Count);
            foreach (var coordinate in coordinates) chunks.Add(CaptureSnapshot(coordinate));
            requiresFreshBaseline = false;
            InputChanged?.Invoke(new MapInputBatch(descriptor.World, inputGeneration, sourceSession, streamGeneration,
                lastSourceCommit, MapInputBatchKind.Baseline, snapshotChunks: chunks));
        }

        public Task<MapChunkData> LoadAsync(WorldDescriptor world, ChunkCoord coordinate, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (descriptor == null) descriptor = world;
            else if (!descriptor.World.Equals(world.World)) throw new InvalidOperationException("网络表现源不能跨世界复用。");
            int size = world.ChunkSize; var cells = new GridCell[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var position = new CellCoord(checked(coordinate.U * size + x), checked(coordinate.V * size + y));
                if (!world.Bounds.Contains(position)) continue;
                if (!source.Read(position).TryGetCell(out cells[y * size + x])) throw new InvalidOperationException("地图基线尚未完整提交。");
            }
            fingerprints[coordinate] = Fingerprint(cells, coordinate, world);
            return Task.FromResult(new MapChunkData(coordinate, cells, readOnly: true));
        }

        /// <summary>手动诊断兼容入口；仅显式调用时扫描已装载区块，并以完整区块基线修复表现。</summary>
        public void NotifyChanged()
        {
            if (descriptor == null || fingerprints.Count == 0) return;
            var changed = new List<MapInputChunk>(); var coordinates = new List<ChunkCoord>(fingerprints.Keys);
            for (int i = 0; i < coordinates.Count; i++)
            {
                var coordinate = coordinates[i]; ulong current = Fingerprint(coordinate, descriptor.ChunkSize);
                if (fingerprints[coordinate] == current) continue;
                var snapshot = CaptureSnapshot(coordinate); changed.Add(snapshot); fingerprints[coordinate] = current;
            }
            if (changed.Count == 0) return;
            InputChanged?.Invoke(new MapInputBatch(descriptor.World, inputGeneration, sourceSession, streamGeneration,
                lastSourceCommit, MapInputBatchKind.SnapshotUpdate, snapshotChunks: changed));
        }

        /// <summary>在副本提交回调内复制变化最终值，退出回调后由预览主线程排队安装。</summary>
        public void NotifyChanged(MapReplicaChange transition)
        {
            if (transition == null) throw new ArgumentNullException(nameof(transition));
            if (transition.Kind == MapReplicaChangeKind.WorldReset)
            {
                inputGeneration = checked(inputGeneration + 1); sourceSession = transition.Session; streamGeneration = transition.StreamGeneration;
                streamIdentityKnown = true; lastSourceCommit = 0; requiresFreshBaseline = true;
                WorldIdentity revokedWorld = descriptor == null || descriptor.World.WorldId.IsEmpty ? transition.World : descriptor.World;
                InputChanged?.Invoke(new MapInputBatch(revokedWorld, inputGeneration, sourceSession, streamGeneration,
                    0, MapInputBatchKind.Reset));
                return;
            }
            if (transition.Kind == MapReplicaChangeKind.VisibilityRevoked || transition.Kind == MapReplicaChangeKind.Disconnected)
            {
                inputGeneration = checked(inputGeneration + 1); sourceSession = transition.Session; streamGeneration = transition.StreamGeneration;
                streamIdentityKnown = true; lastSourceCommit = 0; requiresFreshBaseline = true;
                MapInputBatchKind kind = transition.Kind == MapReplicaChangeKind.VisibilityRevoked ?
                    MapInputBatchKind.VisibilityRevoked : MapInputBatchKind.Disconnected;
                InputChanged?.Invoke(new MapInputBatch(transition.World, inputGeneration, sourceSession, streamGeneration,
                    transition.Commit, kind));
                return;
            }

            if (descriptor == null) throw new InvalidOperationException("完整地形基线尚未建立。");
            if (!transition.World.Equals(descriptor.World))
            {
                inputGeneration = checked(inputGeneration + 1); sourceSession = transition.Session; streamGeneration = transition.StreamGeneration;
                streamIdentityKnown = true; lastSourceCommit = 0; requiresFreshBaseline = true;
                InputChanged?.Invoke(new MapInputBatch(transition.World, inputGeneration, sourceSession, streamGeneration, 0, MapInputBatchKind.Reset));
                return;
            }
            bool streamChanged = streamIdentityKnown && (sourceSession != transition.Session || streamGeneration != transition.StreamGeneration);
            if (streamChanged)
            {
                inputGeneration = checked(inputGeneration + 1); lastSourceCommit = 0; requiresFreshBaseline = true;
            }
            sourceSession = transition.Session; streamGeneration = transition.StreamGeneration; streamIdentityKnown = true;

            if (requiresFreshBaseline)
            {
                TryPublishFreshBaseline(transition.Commit);
                return;
            }

            var cells = new List<MapInputCell>(transition.Cells.Count);
            foreach (var position in transition.Cells)
            {
                if (!descriptor.Bounds.Contains(position)) continue;
                if (!source.Read(position).TryGetCell(out var value))
                {
                    RequestCompleteBaseline(transition.Commit);
                    return;
                }
                cells.Add(new MapInputCell(position, value));
            }
            var snapshots = new List<MapInputChunk>(transition.SnapshotChunks.Count);
            foreach (var coordinate in transition.SnapshotChunks) snapshots.Add(CaptureSnapshot(coordinate));
            if (cells.Count == 0 && snapshots.Count == 0) { lastSourceCommit = transition.Commit; return; }
            MapInputBatchKind batchKind = snapshots.Count == 0 ? MapInputBatchKind.Delta :
                IsCompleteSnapshotSet(snapshots) ? MapInputBatchKind.Baseline : MapInputBatchKind.SnapshotUpdate;
            var batch = new MapInputBatch(transition.World, inputGeneration, transition.Session, transition.StreamGeneration,
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
            if (received.Count != fingerprints.Count) return false;
            foreach (var coordinate in fingerprints.Keys) if (!received.Contains(coordinate)) return false;
            return true;
        }

        private MapInputChunk CaptureSnapshot(ChunkCoord coordinate)
        {
            if (TryCaptureSnapshot(coordinate, out var snapshot)) return snapshot;
            throw new InvalidOperationException("网络快照区块内部含 Unknown，不能转为空格。");
        }

        private ulong Fingerprint(ChunkCoord coordinate, int size)
        {
            const ulong offset = 1469598103934665603UL, prime = 1099511628211UL;
            ulong result = offset;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var position = new CellCoord(checked(coordinate.U * size + x), checked(coordinate.V * size + y));
                var sample = source.Read(position);
                bool known = sample.TryGetCell(out var cell) && descriptor.Bounds.Contains(position);
                result ^= known ? 1UL : 0UL; result *= prime;
                if (!known) continue;
                result ^= cell.TileId; result *= prime;
                result ^= unchecked((ulong)(ushort)cell.Height); result *= prime;
                result ^= cell.Flags; result *= prime;
            }
            return result;
        }

        private static ulong Fingerprint(GridCell[] cells, ChunkCoord coordinate, WorldDescriptor world)
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
