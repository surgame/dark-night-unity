namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>破坏范围中的纯坐标偏移；不包含地图坐标系或运行时引用。</summary>
    public readonly struct TerrainOffset
    {
        public int X { get; }
        public int Y { get; }
        public TerrainOffset(int x, int y) { X = x; Y = y; }
    }
}
