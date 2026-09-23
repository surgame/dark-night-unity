using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Unity;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.View.Terrain
{
    /// <summary>蓝图格子的只读表现源；可提交受约束的预览格副本，并按变化区块通知同一表现链刷新。</summary>
    public sealed class TerrainBlueprintSource : ITerrainChunkChangeSource
    {
        private readonly TerrainBlueprint blueprint;
        private readonly uint[] tiles = new uint[9];
        private readonly object gate = new object();
        private byte[] materials, shapes;
        private int chunkSize;
        public event Action<IReadOnlyList<ChunkCoord>> Changed;
        public TerrainBlueprintSource(TerrainBlueprint blueprint, TileCatalog catalog)
        {
            this.blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            materials = blueprint.CopyMaterials(); shapes = blueprint.CopyShapes();
            string[] keys = { "loam", "slate", "basalt", "copper", "iron", "gold", "moss", "bedrock" };
            for (int i = 0; i < keys.Length; i++) tiles[i + 1] = catalog.ByKey(keys[i]);
        }
        public Task<MapChunkData> LoadAsync(WorldDescriptor descriptor, ChunkCoord coordinate, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            int size = descriptor.ChunkSize; var cells = new GridCell[size * size];
            lock (gate)
            {
                chunkSize = size;
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    int u = coordinate.U * size + x, v = coordinate.V * size + y;
                    if (u < 0 || u >= blueprint.Width || -v < 0 || -v >= blueprint.Height) continue;
                    int index = -v * blueprint.Width + u;
                    byte material = materials[index];
                    if (material != 0)
                    {
                        ushort flags = (ushort)(((int)shapes[index] << 1) | (blueprint.IsProtected(u, -v) ? 1 : 0));
                        cells[y * size + x] = new GridCell(tiles[material], 0, flags);
                    }
                }
            }
            return Task.FromResult(new MapChunkData(coordinate, cells));
        }

        /// <summary>用编辑草稿替换输入格副本；仅变化区块会触发表现刷新，原始蓝图及资产保持不变。</summary>
        public void ReplaceCells(byte[] nextMaterials, byte[] nextShapes)
        {
            int count = blueprint.Width * blueprint.Height;
            if (nextMaterials == null || nextMaterials.Length != count || nextShapes == null || nextShapes.Length != count)
                throw new ArgumentException("地形草稿尺寸无效。");
            var changed = new HashSet<ChunkCoord>();
            lock (gate)
            {
                for (int i = 0; i < count; i++)
                {
                    if (nextMaterials[i] > 8 || nextShapes[i] > 12 ||
                        (nextShapes[i] != 0 && (nextMaterials[i] == 0 || blueprint.IsProtected(i % blueprint.Width, i / blueprint.Width))))
                        throw new ArgumentException("地形草稿材料或坡形无效。");
                    if (materials[i] == nextMaterials[i] && shapes[i] == nextShapes[i]) continue;
                    int x = i % blueprint.Width, row = i / blueprint.Width;
                    if (blueprint.IsProtected(x, row) &&
                        (nextMaterials[i] != blueprint.MaterialAt(x, row) || nextShapes[i] != (byte)blueprint.ShapeAt(x, row)))
                        throw new InvalidOperationException("受保护的地图格不能编辑。");
                    if (chunkSize > 0) changed.Add(new ChunkCoord(GridMath.FloorDiv(x, chunkSize), GridMath.FloorDiv(-row, chunkSize)));
                }
                materials = (byte[])nextMaterials.Clone(); shapes = (byte[])nextShapes.Clone();
            }
            if (changed.Count != 0) Changed?.Invoke(new List<ChunkCoord>(changed).AsReadOnly());
        }
    }
}
