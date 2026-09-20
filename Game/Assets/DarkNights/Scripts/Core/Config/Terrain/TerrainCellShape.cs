namespace DarkNights.Core.Config.Terrain
{
    /// <summary>独立格形状；零为整格，坡形与材料分离。编号写入格 Flags 的第 1–4 位，位置按格中心约定。</summary>
    public enum TerrainCellShape : byte
    {
        Full = 0, Rise = 1, Fall = 2, RiseLow = 3, RiseHigh = 4, FallHigh = 5, FallLow = 6,
        CeilingRise = 7, CeilingFall = 8, CeilingRiseLow = 9, CeilingRiseHigh = 10,
        CeilingFallHigh = 11, CeilingFallLow = 12
    }
}
