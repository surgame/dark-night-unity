using AnyRules.Next;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>将生成蓝图一次性装载到唯一权威地图；材料身份按目录键映射，空格和边缘 padding 始终为已知 Empty。</summary>
    internal static class TerrainMapInitialization
    {
        internal static void Load(ARDMap map, TerrainBlueprint blueprint)
        {
            string[] keys = { "loam", "slate", "basalt", "copper", "iron", "gold", "moss", "bedrock" };
            var tiles = new uint[9];
            for (int i = 0; i < keys.Length; i++) tiles[i + 1] = map.Tiles.ByKey(keys[i]);
            int size = map.Descriptor.ChunkSize;
            for (int cv = GridMath.FloorDiv(-blueprint.Height + 1, size); cv <= 0; cv++)
                for (int cu = 0; cu <= (blueprint.Width - 1) / size; cu++)
                {
                    var cells = new GridCell[size * size];
                    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    {
                        int u = cu * size + x, v = cv * size + y;
                        if (u >= blueprint.Width || -v < 0 || -v >= blueprint.Height) continue;
                        byte t = blueprint.MaterialAt(u, -v);
                        if (t != 0) cells[y * size + x] = new GridCell(tiles[t], 0, (ushort)(blueprint.IsProtected(u, -v) ? 1 : 0));
                    }
                    map.LoadChunk(new ChunkCoord(cu, cv), cells);
                }
        }
    }
}
