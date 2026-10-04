using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Unity;

namespace DarkNights.View.Terrain
{
    /// <summary>矿床冻结占用格的只读原生输入；只拥有表现副本，完整基线包含已知空格，普通变化不重烘背景或结算采集。</summary>
    public sealed class MineralLayerInputSource : ITerrainInputSource
    {
        private readonly TileCatalog catalog;
        private readonly object gate = new object();
        private readonly HashSet<ChunkCoord> loaded = new HashSet<ChunkCoord>();
        private Dictionary<CellCoord, GridCell> cells = new Dictionary<CellCoord, GridCell>();
        private WorldDescriptor descriptor;
        private ulong commit;
        private bool published;
        public event Action<MapInputBatch> InputChanged;
        public ulong SourceCommit { get { lock (gate) return commit; } }

        public MineralLayerInputSource(TileCatalog catalog)
        { this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }

        public Task<MapChunkData> LoadAsync(WorldDescriptor world, ChunkCoord chunk, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            lock (gate)
            {
                if (descriptor == null)
                {
                    foreach (var position in cells.Keys) if (!world.Bounds.Contains(position)) throw new ArgumentException("初始矿格超出地图。");
                    descriptor = world;
                }
                else if (!descriptor.World.Equals(world.World)) throw new InvalidOperationException("矿层输入不能跨世界复用。");
                loaded.Add(chunk);
                return Task.FromResult(new MapChunkData(chunk, CopyChunk(chunk), readOnly: true));
            }
        }

        public void PublishInitialBaseline()
        {
            MapInputBatch batch;
            lock (gate)
            {
                if (descriptor == null || loaded.Count == 0) throw new InvalidOperationException("矿层尚未装载基线区块。");
                var chunks = new List<MapInputChunk>(loaded.Count);
                foreach (var chunk in loaded) chunks.Add(new MapInputChunk(chunk, CopyChunk(chunk)));
                batch = new MapInputBatch(descriptor.World, 1, 0, 0, commit, MapInputBatchKind.Baseline, snapshotChunks: chunks);
                published = true;
            }
            InputChanged?.Invoke(batch);
        }

        /// <summary>验证后原子替换冻结占用；仅为真正变化的矿格发布原生 Delta，扣耐久但占用不变时不产生新游标。</summary>
        public void Replace(IReadOnlyList<MapInputCell> frozen)
        {
            if (frozen == null || frozen.Count > 4096) throw new ArgumentOutOfRangeException(nameof(frozen));
            var next = new Dictionary<CellCoord, GridCell>(frozen.Count);
            foreach (var cell in frozen)
            {
                if (cell.Value.IsEmpty || !catalog.Contains(cell.Value.TileId) || cell.Value.Flags != 0 ||
                    !next.TryAdd(cell.Position, cell.Value)) throw new ArgumentException("矿格重复、未知或包含非法状态。", nameof(frozen));
            }
            MapInputBatch batch = null;
            lock (gate)
            {
                var changes = new List<MapInputCell>();
                foreach (var pair in next)
                {
                    if (descriptor != null && !descriptor.Bounds.Contains(pair.Key)) throw new ArgumentException("矿格超出地图。");
                    if (!cells.TryGetValue(pair.Key, out var old) || old != pair.Value) changes.Add(new MapInputCell(pair.Key, pair.Value));
                }
                foreach (var pair in cells) if (!next.ContainsKey(pair.Key)) changes.Add(new MapInputCell(pair.Key, default));
                if (changes.Count == 0) return;
                cells = next; commit = checked(commit + 1);
                if (published) batch = new MapInputBatch(descriptor.World, 1, 0, 0, commit, MapInputBatchKind.Delta, changes);
            }
            if (batch != null) InputChanged?.Invoke(batch);
        }

        private GridCell[] CopyChunk(ChunkCoord chunk)
        {
            int size = descriptor.ChunkSize;
            var result = new GridCell[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var position = new CellCoord(chunk.U * size + x, chunk.V * size + y);
                if (descriptor.Bounds.Contains(position) && cells.TryGetValue(position, out var cell)) result[y * size + x] = cell;
            }
            return result;
        }
    }
}
