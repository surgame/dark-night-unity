namespace DarkNights.Core.Config.Terrain
{
    /// <summary>矿工任务的地图格子地址；负数域与正数实体身份区分，零表示无任务，地址只在当前世界内有效。</summary>
    public static class MineralTaskTarget
    {
        public static int Encode(int u, int v) => -(1 + -v * TerrainGenerationSettings.Width + u);
        public static bool TryDecode(int value, out int u, out int v)
        {
            long index = -(long)value - 1;
            u = 0; v = 0;
            if (index < 0 || index >= TerrainGenerationSettings.Width * TerrainGenerationSettings.Height) return false;
            u = (int)index % TerrainGenerationSettings.Width; v = -(int)index / TerrainGenerationSettings.Width; return true;
        }
    }
}
