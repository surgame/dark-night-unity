namespace DarkNights.Core.Config.Terrain
{
    /// <summary>矿床不可变初始分配格；使用地图 U/V 坐标和容量，不包含运行剩余量、表现 TileId 或网络身份。</summary>
    public readonly struct TerrainMineralCell
    {
        public int U { get; }
        public int V { get; }
        public int Capacity { get; }

        public TerrainMineralCell(int u, int v, int capacity)
        {
            if (u < 0 || u >= TerrainGenerationSettings.Width || v > 0 || v <= -TerrainGenerationSettings.Height || capacity < 1)
                throw new System.ArgumentOutOfRangeException(nameof(capacity), "矿格坐标或容量无效。");
            U = u; V = v; Capacity = capacity;
        }
    }
}
