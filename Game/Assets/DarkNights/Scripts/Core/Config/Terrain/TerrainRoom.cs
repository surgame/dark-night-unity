namespace DarkNights.Core.Config.Terrain
{
    /// <summary>生成阶段冻结的洞室空间信息；坐标沿用参考文件的右向 X、下向 Y，不持有运行时状态。</summary>
    public sealed class TerrainRoom
    {
        public string Kind { get; }
        public int X { get; }
        public int Y { get; }
        public int Left { get; }
        public int Top { get; }
        public int Width { get; }
        public int Height { get; }
        public TerrainRoomFeature Features { get; }
        public int DepositBudget { get; }
        public int SoftRockRadius { get; }

        public TerrainRoom(string kind, int x, int y, int width, int height)
            : this(kind, x, y, width, height, TerrainRoomFeature.None, 0, 0)
        {
        }

        public TerrainRoom(string kind, int x, int y, int width, int height,
            TerrainRoomFeature features, int depositBudget, int softRockRadius)
        {
            Kind = kind; X = x; Y = y; Width = width; Height = height;
            Left = x - width / 2; Top = y - height / 2;
            Features = features; DepositBudget = depositBudget; SoftRockRadius = softRockRadius;
        }
    }
}
