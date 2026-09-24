using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Networking;
using AnyRules.Next.Unity;

namespace DarkNights.View.Terrain
{
    /// <summary>把已经原子提交的网络只读格子复制为本地页面输入；所有渲染区块标记只读，不生成地图或开放编辑能力。</summary>
    public sealed class TerrainReplicaSource : ITerrainChunkChangeSource
    {
        private readonly IReadOnlyGrid source;
        private readonly Dictionary<ChunkCoord, ulong> fingerprints = new Dictionary<ChunkCoord, ulong>();
        private WorldDescriptor descriptor;
        public event Action<IReadOnlyList<ChunkCoord>> Changed;
        public TerrainReplicaSource(IReadOnlyGrid source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
        }
        public Task<MapChunkData> LoadAsync(WorldDescriptor descriptor, ChunkCoord coordinate, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (this.descriptor == null) this.descriptor = descriptor;
            else if (!this.descriptor.World.Equals(descriptor.World))
            {
                this.descriptor = descriptor;
                fingerprints.Clear();
            }
            int size = descriptor.ChunkSize;
            var cells = new GridCell[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var position = new CellCoord(coordinate.U * size + x, coordinate.V * size + y);
                if (!descriptor.Bounds.Contains(position)) continue;
                if (!source.Read(position).TryGetCell(out cells[y * size + x])) throw new InvalidOperationException("地图区块尚未完整提交。");
            }
            fingerprints[coordinate] = Fingerprint(cells, coordinate, descriptor);
            return Task.FromResult(new MapChunkData(coordinate, cells, readOnly: true));
        }

        /// <summary>比较副本的 Chunk 指纹，只把本次提交影响的 Chunk 交给页面刷新器。</summary>
        public void NotifyChanged()
        {
            if (descriptor == null) return;
            var changed = new List<ChunkCoord>();
            int size = descriptor.ChunkSize;
            int minU = GridMath.FloorDiv(descriptor.Bounds.MinU, size);
            int minV = GridMath.FloorDiv(descriptor.Bounds.MinV, size);
            int maxU = GridMath.FloorDiv(checked((int)descriptor.Bounds.MaxUExclusive - 1), size);
            int maxV = GridMath.FloorDiv(checked((int)descriptor.Bounds.MaxVExclusive - 1), size);
            var current = new Dictionary<ChunkCoord, ulong>();
            for (int v = minV; v <= maxV; v++) for (int u = minU; u <= maxU; u++)
            {
                var coordinate = new ChunkCoord(u, v);
                ulong fingerprint = Fingerprint(coordinate, size);
                current.Add(coordinate, fingerprint);
                if (!fingerprints.TryGetValue(coordinate, out var previous) || previous != fingerprint)
                    changed.Add(coordinate);
            }
            fingerprints.Clear();
            foreach (var pair in current) fingerprints.Add(pair.Key, pair.Value);
            if (changed.Count != 0) Changed?.Invoke(changed.AsReadOnly());
        }

        /// <summary>把已原子安装的副本范围直接传给表现层，不读取其他区块。</summary>
        public void NotifyChanged(MapReplicaChange transition)
        {
            if (transition == null || transition.Kind != MapReplicaChangeKind.CellsChanged ||
                transition.Chunks.Count == 0) return;
            Changed?.Invoke(transition.Chunks);
        }

        private ulong Fingerprint(ChunkCoord coordinate, int size)
        {
            const ulong offset = 1469598103934665603UL;
            const ulong prime = 1099511628211UL;
            ulong result = offset;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var position = new CellCoord(coordinate.U * size + x, coordinate.V * size + y);
                var sample = source.Read(position);
                bool known = sample.TryGetCell(out var cell) && descriptor.Bounds.Contains(position);
                result ^= known ? 1UL : 0UL;
                result *= prime;
                if (!known) continue;
                result ^= cell.TileId; result *= prime;
                result ^= unchecked((ulong)(ushort)cell.Height); result *= prime;
                result ^= cell.Flags; result *= prime;
            }
            return result;
        }

        private static ulong Fingerprint(GridCell[] cells, ChunkCoord coordinate, WorldDescriptor descriptor)
        {
            const ulong offset = 1469598103934665603UL;
            const ulong prime = 1099511628211UL;
            ulong result = offset;
            int size = descriptor.ChunkSize;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var position = new CellCoord(coordinate.U * size + x, coordinate.V * size + y);
                bool known = descriptor.Bounds.Contains(position);
                result ^= known ? 1UL : 0UL; result *= prime;
                if (!known) continue;
                var cell = cells[y * size + x];
                result ^= cell.TileId; result *= prime;
                result ^= unchecked((ulong)(ushort)cell.Height); result *= prime;
                result ^= cell.Flags; result *= prime;
            }
            return result;
        }
    }
}
