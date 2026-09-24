namespace DarkNights.View.Terrain
{
    /// <summary>Editor 蓝图草稿单格的最终材料与坡形；坐标采用蓝图向下为正的行号。</summary>
    public readonly struct TerrainBlueprintCellChange
    {
        public int X { get; }
        public int Row { get; }
        public byte Material { get; }
        public byte Shape { get; }
        public TerrainBlueprintCellChange(int x, int row, byte material, byte shape)
        { X = x; Row = row; Material = material; Shape = shape; }
    }
}
