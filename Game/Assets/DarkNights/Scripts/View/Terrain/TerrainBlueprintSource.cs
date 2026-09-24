using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Unity;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.View.Terrain
{
    /// <summary>固定蓝图的只读表现输入；初始整图只用于装载，后续草稿改动以冻结的稀疏格事务安装。</summary>
    public sealed class TerrainBlueprintSource : ITerrainInputSource
    {
        private readonly TerrainBlueprint blueprint;
        private readonly uint[] tiles = new uint[9];
        private readonly object gate = new object();
        private readonly HashSet<ChunkCoord> loaded = new HashSet<ChunkCoord>();
        private byte[] materials, shapes;
        private WorldDescriptor descriptor;
        private ulong sourceCommit;
        public event Action<MapInputBatch> InputChanged;

        public TerrainBlueprintSource(TerrainBlueprint blueprint, TileCatalog catalog)
        {
            this.blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            materials = blueprint.CopyMaterials(); shapes = blueprint.CopyShapes();
            string[] keys = { "loam", "slate", "basalt", "copper", "iron", "gold", "moss", "bedrock" };
            for (int i = 0; i < keys.Length; i++) tiles[i + 1] = catalog.ByKey(keys[i]);
        }

        public Task<MapChunkData> LoadAsync(WorldDescriptor world, ChunkCoord coordinate, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            lock (gate)
            {
                if (descriptor == null) descriptor = world;
                else if (!descriptor.World.Equals(world.World)) throw new InvalidOperationException("蓝图表现源不能跨世界复用。");
                loaded.Add(coordinate);
            }
            int size = world.ChunkSize; var cells = new GridCell[size * size];
            lock (gate)
            {
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    long u = (long)coordinate.U * size + x, v = (long)coordinate.V * size + y;
                    if (u < 0 || u >= blueprint.Width || -v < 0 || -v >= blueprint.Height) continue;
                    int index = checked((int)-v * blueprint.Width + (int)u);
                    cells[y * size + x] = CellAt(index);
                }
            }
            return Task.FromResult(new MapChunkData(coordinate, cells, readOnly: true));
        }

        public void PublishInitialBaseline()
        {
            MapInputBatch batch;
            lock (gate)
            {
                if (descriptor == null || loaded.Count == 0) throw new InvalidOperationException("蓝图区块尚未完成初始装载。");
                var chunks = new List<MapInputChunk>(loaded.Count);
                foreach (var coordinate in loaded)
                {
                    int size = descriptor.ChunkSize; var cells = new GridCell[size * size];
                    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    {
                        long u = (long)coordinate.U * size + x, v = (long)coordinate.V * size + y;
                        if (u < 0 || u >= blueprint.Width || -v < 0 || -v >= blueprint.Height) continue;
                        cells[y * size + x] = CellAt(checked((int)-v * blueprint.Width + (int)u));
                    }
                    chunks.Add(new MapInputChunk(coordinate, cells));
                }
                batch = new MapInputBatch(descriptor.World, 1, 0, 0, sourceCommit, MapInputBatchKind.Baseline, snapshotChunks: chunks);
            }
            InputChanged?.Invoke(batch);
        }

        /// <summary>显式全量替换入口，只用于打开草稿或重建预览；普通绘制不得调用此路径。</summary>
        public void ReplaceCells(byte[] nextMaterials, byte[] nextShapes)
        {
            int count = checked(blueprint.Width * blueprint.Height);
            if (nextMaterials == null || nextMaterials.Length != count || nextShapes == null || nextShapes.Length != count)
                throw new ArgumentException("地形草稿尺寸无效。");
            var changes = new List<TerrainBlueprintCellChange>();
            lock (gate)
            {
                for (int i = 0; i < count; i++)
                {
                    byte material = nextMaterials[i], shape = nextShapes[i];
                    ValidateCell(i % blueprint.Width, i / blueprint.Width, material, shape);
                    if (materials[i] != material || shapes[i] != shape)
                        changes.Add(new TerrainBlueprintCellChange(i % blueprint.Width, i / blueprint.Width, material, shape));
                }
            }
            ApplyChanges(changes);
        }

        /// <summary>在一次锁内验证并写入稀疏草稿，净变化为空时不发布新源提交。</summary>
        public void ApplyChanges(IReadOnlyList<TerrainBlueprintCellChange> changes)
        {
            if (changes == null || changes.Count == 0) return;
            var final = new Dictionary<int, TerrainBlueprintCellChange>(changes.Count);
            MapInputBatch batch = null;
            lock (gate)
            {
                for (int i = 0; i < changes.Count; i++)
                {
                    var change = changes[i];
                    ValidateCell(change.X, change.Row, change.Material, change.Shape);
                    final[change.Row * blueprint.Width + change.X] = change;
                }
                var values = new List<MapInputCell>(final.Count);
                foreach (var pair in final)
                {
                    int index = pair.Key; var change = pair.Value;
                    if (materials[index] == change.Material && shapes[index] == change.Shape) continue;
                    materials[index] = change.Material; shapes[index] = change.Shape;
                    values.Add(new MapInputCell(new CellCoord(change.X, -change.Row), CellAt(index)));
                }
                if (values.Count == 0 || descriptor == null) return;
                batch = new MapInputBatch(descriptor.World, 1, 0, 0, checked(++sourceCommit), MapInputBatchKind.Delta, values);
            }
            InputChanged?.Invoke(batch);
        }

        private void ValidateCell(int x, int row, byte material, byte shape)
        {
            if (x < 0 || x >= blueprint.Width || row < 0 || row >= blueprint.Height || material > 8 || shape > 12 ||
                (shape != 0 && material == 0) || (shape != 0 && blueprint.IsProtected(x, row)))
                throw new ArgumentException("地形草稿材料、坡形或坐标无效。");
            if (blueprint.IsProtected(x, row) &&
                (material != blueprint.MaterialAt(x, row) || shape != (byte)blueprint.ShapeAt(x, row)))
                throw new InvalidOperationException("受保护的地图格不能编辑。");
        }

        private GridCell CellAt(int index)
        {
            byte material = materials[index];
            if (material == 0) return default;
            int x = index % blueprint.Width, row = index / blueprint.Width;
            ushort flags = (ushort)(((int)shapes[index] << 1) | (blueprint.IsProtected(x, row) ? 1 : 0));
            return new GridCell(tiles[material], 0, flags);
        }
    }
}
