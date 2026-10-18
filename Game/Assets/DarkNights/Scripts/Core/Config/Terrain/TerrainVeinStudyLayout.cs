using System;
using System.Collections.Generic;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>
    /// 单个近壁制作候选的冻结剖面；采样来自当前完整地图，矿层与前景可占同一格。
    /// 此对象仅供编辑态审阅，不拥有运行状态；巢腔、封岩和支撑属于本候选而非作者源。
    /// </summary>
    public sealed class TerrainVeinStudyLayout
    {
        public const int Width = 20, Height = 16, EntryColumn = 4, EntryRow = 8;
        private readonly byte[] materials;
        private readonly int[] capacities;
        public int WorldX { get; }
        public int WorldY { get; }
        public bool HasNest { get; }
        public int TotalCapacity { get; }
        public int MineralCells { get; }
        public int SiteCount { get; }
        public string SourceSeed { get; }
        public TerrainVeinShape Shape { get; }
        public IReadOnlyList<TerrainDepositBlueprint> Deposits { get; }

        public TerrainVeinStudyLayout(int worldX, int worldY, string sourceSeed, TerrainVeinShape shape,
            bool hasNest, int siteCount, byte[] materials, int[] capacities, TerrainDepositBlueprint[] deposits)
        {
            if (materials == null || capacities == null || materials.Length != Width * Height ||
                capacities.Length != materials.Length || deposits == null || siteCount < 1)
                throw new ArgumentException("矿巢候选的剖面或来源无效。");
            this.materials = (byte[])materials.Clone(); this.capacities = (int[])capacities.Clone();
            WorldX = worldX; WorldY = worldY; SourceSeed = sourceSeed; Shape = shape;
            HasNest = hasNest; SiteCount = siteCount;
            int total = 0, count = 0;
            foreach (int value in capacities)
            {
                if (value < 0) throw new ArgumentException("矿格容量不能为负。");
                total = checked(total + value); if (value > 0) count++;
            }
            TotalCapacity = total; MineralCells = count;
            Deposits = Array.AsReadOnly((TerrainDepositBlueprint[])deposits.Clone());
        }

        public byte Material(int x, int y) => materials[y * Width + x];
        public int Capacity(int x, int y) => capacities[y * Width + x];
        public bool Pocket(int x, int y) => HasNest && x >= 8 && x <= 11 && y >= 6 && y <= 8;
        public bool Passage(int x, int y, int columns) => y >= 7 && y <= 8 && x >= 4 && x < 4 + columns;
        public int WorldColumn(int localX) => WorldX + localX - EntryColumn;
        public int WorldRow(int localY) => WorldY + localY - EntryRow;
    }
}
