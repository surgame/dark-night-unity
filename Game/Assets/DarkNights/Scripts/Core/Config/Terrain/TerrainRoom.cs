namespace DarkNights.Core.Config.Terrain
{
    /// <summary>生成阶段冻结的洞室空间信息；坐标沿用参考文件的右向 X、下向 Y，不持有探索或战斗状态。</summary>
    public sealed class TerrainRoom
    {
        public string Kind { get; }
        public int X { get; }
        public int Y { get; }
        public int Left { get; }
        public int Top { get; }
        public int Width { get; }
        public int Height { get; }
        public TerrainRoom(string kind, int x, int y, int width, int height)
        {
            Kind = kind; X = x; Y = y; Width = width; Height = height;
            Left = x - width / 2; Top = y - height / 2;
        }
    }
}
