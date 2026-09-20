using System;
using System.Collections.Generic;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>一次生成的冻结蓝图；仅用于初始化或编辑器烘焙。运行时修改归 AnyRuleD，不能写回蓝图。</summary>
    public sealed class TerrainBlueprint
    {
        private readonly byte[] cells;
        private readonly bool[] protectedCells;
        private readonly bool[] softRock;
        private readonly TerrainGenerationSettings settings;
        public int Width => TerrainGenerationSettings.Width;
        public int Height => TerrainGenerationSettings.Height;
        public TerrainGenerationSettings Settings => settings.CopyValidated();
        public IReadOnlyList<int> Surface { get; }
        public IReadOnlyList<TerrainRoom> Rooms { get; }
        public IReadOnlyList<TerrainDepositBlueprint> Deposits { get; }
        public int SoftRockCount { get; }
        public IReadOnlyList<CavePassage> Passages { get; }

        public TerrainBlueprint(TerrainGenerationSettings settings, byte[] cells, bool[] protectedCells,
            int[] surface, TerrainRoom[] rooms)
            : this(settings, cells, protectedCells, surface, rooms, new bool[cells == null ? 0 : cells.Length],
                Array.Empty<TerrainDepositBlueprint>())
        {
        }

        public TerrainBlueprint(TerrainGenerationSettings settings, byte[] cells, bool[] protectedCells,
            int[] surface, TerrainRoom[] rooms, bool[] softRock, TerrainDepositBlueprint[] deposits, CavePassage[] passages = null)
        {
            this.settings = settings.CopyValidated();
            Passages = Array.AsReadOnly((CavePassage[])(passages ?? Array.Empty<CavePassage>()).Clone());
            if (cells == null || cells.Length != Width * Height || protectedCells == null ||
                protectedCells.Length != cells.Length || surface == null || surface.Length != Width || rooms == null ||
                softRock == null || softRock.Length != cells.Length || deposits == null)
                throw new ArgumentException("蓝图尺寸不完整。");
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] > 8 || (softRock[i] && (cells[i] == 8 || protectedCells[i])))
                    throw new ArgumentException("未知地形编号或软岩标记。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TerrainDepositBlueprint deposit in deposits)
            {
                if (deposit == null || deposit.X < 0 || deposit.X >= Width || deposit.Y < 0 || deposit.Y >= Height ||
                    !ids.Add(deposit.Id)) throw new ArgumentException("矿床静态标记重复或越界。");
            }
            this.cells = (byte[])cells.Clone(); this.protectedCells = (bool[])protectedCells.Clone();
            this.softRock = (bool[])softRock.Clone();
            SoftRockCount = 0;
            for (int i = 0; i < this.softRock.Length; i++) if (this.softRock[i]) SoftRockCount++;
            Surface = Array.AsReadOnly((int[])surface.Clone()); Rooms = Array.AsReadOnly((TerrainRoom[])rooms.Clone());
            Deposits = Array.AsReadOnly((TerrainDepositBlueprint[])deposits.Clone());
        }
        public byte MaterialAt(int x, int y) => cells[Index(x, y)];
        public bool IsProtected(int x, int y) => protectedCells[Index(x, y)];
        public bool IsSoftRock(int x, int y) => softRock[Index(x, y)];
        public byte[] CopyMaterials() => (byte[])cells.Clone();
        public bool[] CopySoftRock() => (bool[])softRock.Clone();
        private int Index(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(x));
            return y * Width + x;
        }
    }
}
