using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>
    /// 开发端联合候选配方：只在真实近壁围岩布置厚矿脉和可选侧巢；覆盖处替换原矿，其他原矿保留。
    /// 每张输出冻结，不在开局或开挖时抽签；空间不足拒绝，已有地图与已采空状态不得当作生成输入。
    /// </summary>
    public static class JointMineCandidate
    {
        public const string Version = "joint-thick-veins-side-nests-v1";
        public static PlayableTerrain Generate(PlayableTerrain source, int count = 6, bool nests = true)
        {
            if (source == null || source.Nests.Count != 0 || source.Minerals != null || source.Damage.Count != 0 || count < 1 || count > 16)
                throw new ArgumentException("联合配方只接受未运行的基础地图，矿脉数量为1–16。");
            var current = new PlayableTerrain(source.WorldId, source.Seed, source.CopyMaterials(), source.CopyProtection(),
                source.CopySoftRock(), source.Rooms.ToArray(), Array.Empty<TerrainDepositBlueprint>(), source.CopyShapes(), source.Expedition, source.Background);
            var sites = new List<TerrainNest>();
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                TerrainVeinStudyLayout layout;
                try { layout = TerrainVeinStudyGenerator.Generate(current, (TerrainVeinShape)(i % 4), 96 + i * 32, i % 3 != 1, i * 37, i, i % 3 != 1); }
                catch (InvalidOperationException)
                {
                    try { layout = TerrainVeinStudyGenerator.Generate(current, (TerrainVeinShape)(i % 4), 96 + i * 32, false, i * 37, i, false); }
                    catch (InvalidOperationException) { break; }
                }
                var cells = current.CopyMaterials(); var shapes = current.CopyShapes(); var soft = current.CopySoftRock();
                if (layout.HasNest)
                {
                    var nest = new TerrainNest(sites.Count + 1, layout.WorldX + 3, layout.WorldY);
                    for (int y = nest.Row - 2; y <= nest.Row; y++) for (int x = nest.U + 1; x <= nest.U + 4; x++)
                    { int index = y * TerrainGenerationSettings.Width + x; cells[index] = 0; shapes[index] = 0; soft[index] = false; }
                    sites.Add(nest);
                }
                var deposits = current.Deposits.Concat(layout.Deposits.Select((d, k) => new TerrainDepositBlueprint(
                    "joint-" + i + "-" + k, d.RoomKind, d.X, d.Y, d.Rarity, d.Capacity, d.Cells))).ToArray();
                var background = new BackgroundBakeDescriptor(source.WorldId, source.Seed, cells, shapes);
                current = new PlayableTerrain(source.WorldId, source.Seed, cells, source.CopyProtection(), soft,
                    source.Rooms.ToArray(), deposits, shapes, source.Expedition, background);
                placed++;
            }
            if (placed == 0) throw new InvalidOperationException("没有满足角色初始净空与连续支撑的联合矿巢样点。");
            var occupied = new HashSet<int>(current.Deposits.SelectMany(d => d.Cells).Select(c => -c.V * TerrainGenerationSettings.Width + c.U));
            var retained = new List<TerrainDepositBlueprint>();
            foreach (var deposit in source.Deposits)
            {
                var cells = deposit.Cells.Where(c => !occupied.Contains(-c.V * TerrainGenerationSettings.Width + c.U)).ToArray();
                if (cells.Length != 0) retained.Add(new TerrainDepositBlueprint(deposit.Id, deposit.RoomKind,
                    cells[0].U, -cells[0].V, deposit.Rarity, cells.Sum(c => c.Capacity), cells));
            }
            return new PlayableTerrain(current.WorldId, current.Seed, nests ? current.CopyMaterials() : source.CopyMaterials(),
                current.CopyProtection(), nests ? current.CopySoftRock() : source.CopySoftRock(), current.Rooms.ToArray(),
                retained.Concat(current.Deposits).ToArray(), nests ? current.CopyShapes() : source.CopyShapes(), current.Expedition,
                nests ? current.Background : source.Background, nests: nests ? sites.ToArray() : Array.Empty<TerrainNest>());
        }
    }
}
