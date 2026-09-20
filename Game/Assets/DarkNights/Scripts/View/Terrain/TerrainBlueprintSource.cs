using System;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Unity;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.View.Terrain
{
    /// <summary>编辑器及离线预览的冻结初始格源；按区块读取，H5 的向下 Y 在此唯一转换为 Unity 向上 V。</summary>
    public sealed class TerrainBlueprintSource : IMapChunkSource
    {
        private readonly TerrainBlueprint blueprint;
        private readonly uint[] tiles = new uint[9];
        public TerrainBlueprintSource(TerrainBlueprint blueprint, TileCatalog catalog)
        {
            this.blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            string[] keys = { "loam", "slate", "basalt", "copper", "iron", "gold", "moss", "bedrock" };
            for (int i = 0; i < keys.Length; i++) tiles[i + 1] = catalog.ByKey(keys[i]);
        }
        public Task<MapChunkData> LoadAsync(WorldDescriptor descriptor, ChunkCoord coordinate, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            int size = descriptor.ChunkSize; var cells = new GridCell[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int u = coordinate.U * size + x, v = coordinate.V * size + y;
                if (u < 0 || u >= blueprint.Width || -v < 0 || -v >= blueprint.Height) continue;
                byte material = blueprint.MaterialAt(u, -v);
                if (material != 0) cells[y * size + x] = new GridCell(tiles[material], 0, blueprint.CellFlagsAt(u, -v));
            }
            return Task.FromResult(new MapChunkData(coordinate, cells));
        }
    }
}
