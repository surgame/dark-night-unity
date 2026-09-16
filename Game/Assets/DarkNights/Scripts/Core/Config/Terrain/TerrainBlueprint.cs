using System;
using System.Collections.Generic;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>一次生成的冻结蓝图；仅用于初始化或编辑器烘焙。运行时修改归 AnyRuleD，不能写回蓝图。</summary>
    public sealed class TerrainBlueprint
    {
        private readonly byte[] cells;
        private readonly bool[] protectedCells;
        private readonly TerrainGenerationSettings settings;
        public int Width => TerrainGenerationSettings.Width;
        public int Height => TerrainGenerationSettings.Height;
        public TerrainGenerationSettings Settings => settings.CopyValidated();
        public IReadOnlyList<int> Surface { get; }
        public IReadOnlyList<TerrainRoom> Rooms { get; }
        public TerrainBlueprint(TerrainGenerationSettings settings, byte[] cells, bool[] protectedCells,
            int[] surface, TerrainRoom[] rooms)
        {
            this.settings = settings.CopyValidated();
            if (cells == null || cells.Length != Width * Height || protectedCells == null ||
                protectedCells.Length != cells.Length || surface == null || surface.Length != Width || rooms == null)
                throw new ArgumentException("蓝图尺寸不完整。");
            foreach (byte cell in cells) if (cell > 8) throw new ArgumentException("未知地形编号。");
            this.cells = (byte[])cells.Clone(); this.protectedCells = (bool[])protectedCells.Clone();
            Surface = Array.AsReadOnly((int[])surface.Clone()); Rooms = Array.AsReadOnly((TerrainRoom[])rooms.Clone());
        }
        public byte MaterialAt(int x, int y) => cells[Index(x, y)];
        public bool IsProtected(int x, int y) => protectedCells[Index(x, y)];
        public byte[] CopyMaterials() => (byte[])cells.Clone();
        private int Index(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(x));
            return y * Width + x;
        }
    }
}
