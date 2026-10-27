using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>
    /// 生产阶段的只读变化回执；数量由真实数组比较得出，坐标为地图格，空变化范围为-1。
    /// 可选材料及坡形副本只用于步骤预览，默认不随候选常驻，也不进入运行协议或玩家存档。
    /// </summary>
    public sealed class TerrainGenerationStep
    {
        private readonly byte[] materials;
        private readonly byte[] shapes;
        public string Name { get; }
        public int ChangedCells { get; }
        public int ChangedShapes { get; }
        public int MineralCells { get; }
        public int Nests { get; }
        public int Left { get; }
        public int Top { get; }
        public int Right { get; }
        public int Bottom { get; }
        public bool HasSnapshot => materials != null;

        public TerrainGenerationStep(string name, int changed, int changedShapes, int mineralCells, int nests,
            int left, int top, int right, int bottom, byte[] cells = null, byte[] geometry = null)
        {
            Name = name; ChangedCells = changed; ChangedShapes = changedShapes;
            MineralCells = mineralCells; Nests = nests;
            Left = left; Top = top; Right = right; Bottom = bottom;
            materials = cells == null ? null : (byte[])cells.Clone();
            shapes = geometry == null ? null : (byte[])geometry.Clone();
        }

        public byte[] CopyMaterials() => materials == null ? null : (byte[])materials.Clone();
        public byte[] CopyShapes() => shapes == null ? null : (byte[])shapes.Clone();
    }
}
