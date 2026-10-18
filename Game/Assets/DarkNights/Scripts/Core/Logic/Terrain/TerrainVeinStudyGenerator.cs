using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>
    /// 当前完整地图的近壁制作候选生成器；有界扫描实际围岩、封闭巢腔、出入口与支撑，失败明确报告。
    /// 输出局部冻结剖面及原生矿床初值；绝不回写作者地图，不借矿层占格改变身体碰撞。
    /// </summary>
    public static class TerrainVeinStudyGenerator
    {
        private const int W = TerrainGenerationSettings.Width, H = TerrainGenerationSettings.Height;

        public static TerrainVeinStudyLayout Generate(PlayableTerrain source, TerrainVeinShape shape,
            int capacity, bool hasNest, int siteIndex, int variation, bool requirePocket = true)
        {
            if (source == null || capacity < 4 || capacity > 1000000 || siteIndex < 0 || variation < 0)
                throw new ArgumentException("矿脉预览输入无效。");
            var protection = source.CopyProtection();
            var oldOre = new HashSet<int>();
            foreach (var deposit in source.Deposits)
                foreach (var cell in deposit.Cells) oldOre.Add(-cell.V * W + cell.U);
            var sites = new List<int>();
            for (int y = 43; y < H - 8 && sites.Count < 256; y++)
                for (int x = 12; x < W - 16 && sites.Count < 256; x++)
                    if (Fits(source, protection, oldOre, x, y, requirePocket)) sites.Add(y * W + x);
            if (sites.Count == 0)
                throw new InvalidOperationException("当前地图没有符合近壁、封闭巢腔及连续支撑的样点；未挪动或补造作者地形。");
            int anchor = sites[siteIndex % sites.Count], sx = anchor % W, sy = anchor / W;
            int width = TerrainVeinStudyLayout.Width, height = TerrainVeinStudyLayout.Height;
            var materials = new byte[width * height];
            var capacities = new int[materials.Length];
            var mineralIndices = new List<int>();
            // 相似露头先进入列表；揭露与回放不再重新抽矿量或巢位。
            for (int x = 3; x <= 6; x++) mineralIndices.Add(8 * width + x);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int wx = sx + x - 4, wy = sy + y - 8, i = y * width + x;
                    materials[i] = source.Material(wx, wy);
                    if (y == 8 && x >= 3 && x <= 6) continue;
                    if (TerrainVeinShapeMask.Contains(shape, x, y, variation) &&
                        !protection[wy * W + wx] && source.Material(wx, wy) != 8 && source.Material(wx, wy) != 0 && !oldOre.Contains(wy * W + wx))
                        mineralIndices.Add(i);
                }
            if (capacity < mineralIndices.Count)
                throw new InvalidOperationException("储量低于矿格数（" + mineralIndices.Count + "）；请减小矿形或增加储量，每格至少1单位。");
            int each = capacity / mineralIndices.Count, remainder = capacity % mineralIndices.Count;
            for (int i = 0; i < mineralIndices.Count; i++) capacities[mineralIndices[i]] = each + (i < remainder ? 1 : 0);
            var deposits = new List<TerrainDepositBlueprint>();
            for (int start = 0; start < mineralIndices.Count; start += 64)
            {
                var cells = new List<TerrainMineralCell>(); int amount = 0;
                for (int i = start; i < Math.Min(start + 64, mineralIndices.Count); i++)
                {
                    int index = mineralIndices[i], x = sx + index % width - 4, y = sy + index / width - 8;
                    cells.Add(new TerrainMineralCell(x, -y, capacities[index])); amount += capacities[index];
                }
                var first = cells[0];
                deposits.Add(new TerrainDepositBlueprint("study-" + sx + "-" + sy + "-" + start / 64,
                    "study", first.U, -first.V, "normal", amount, cells));
            }
            return new TerrainVeinStudyLayout(sx, sy, source.Seed, shape, hasNest, sites.Count,
                materials, capacities, deposits.ToArray());
        }

        private static bool Fits(PlayableTerrain source, bool[] protection, HashSet<int> oldOre, int x, int y, bool hasNest)
        {
            for (int dy = -2; dy <= 0; dy++)
                for (int dx = -3; dx <= -1; dx++)
                    if (source.Material(x + dx, y + dy) != 0) return false;
            for (int dx = -3; dx <= (hasNest ? 8 : -1); dx++)
                if (source.Material(x + dx, y + 1) == 0 || protection[(y + 1) * W + x + dx]) return false;
            for (int dy = hasNest ? -2 : 0; dy <= 0; dy++)
                for (int dx = 0; dx <= 2; dx++)
                    if (!Rock(source, protection, oldOre, x + dx, y + dy)) return false;
            if (hasNest)
                for (int dy = -3; dy <= 1; dy++)
                    for (int dx = 3; dx <= 8; dx++)
                        if (!Rock(source, protection, oldOre, x + dx, y + dy)) return false;
            return !oldOre.Contains(y * W + x - 1);
        }

        private static bool Rock(PlayableTerrain source, bool[] protection, HashSet<int> oldOre, int x, int y)
        {
            byte value = source.Material(x, y);
            return value != 0 && value != 8 && !protection[y * W + x] && !oldOre.Contains(y * W + x);
        }
    }
}
