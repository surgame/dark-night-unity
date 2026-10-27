using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>
    /// 在同一基础空间上重放区域矿物覆盖；未选区域沿用原配方的完整数据与身份。
    /// 覆盖各用独立随机流和预算，受保护格、邻区矿层和巢壳不可占用，失败不向邻区借预算。
    /// </summary>
    public static class CaveRegionContent
    {
        private const int W = TerrainGenerationSettings.Width, H = TerrainGenerationSettings.Height;

        public static PlayableTerrain Apply(PlayableTerrain source, PlayableTerrain baseline,
            TerrainGenerationSettings settings, CaveRulePlan plan, Action<string> report)
        {
            if (settings.RegionOverrides == null || settings.RegionOverrides.Length == 0) return baseline;
            var rules = settings.CopyValidated().Structure;
            var minerals = settings.Minerals;
            var deposits = baseline.Deposits.ToList();
            var nests = baseline.Nests.ToList();
            byte[] cells = baseline.CopyMaterials();
            bool[] protection = source.CopyProtection();
            foreach (var input in settings.RegionOverrides.OrderBy(x => x.Region))
            {
                var region = input.CopyValidated();
                if (region.Region >= plan.Rooms.Count || region.SpaceIdentity != rules.SpaceKey(settings.Seed))
                {
                    report?.Invoke("R" + region.Region + "覆盖已过期并保留；本次未应用，需显式重新绑定或清除。");
                    continue;
                }
                string prefix = "rule-r" + region.Region + "-";
                var previous = deposits.Where(d => d.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
                int budget = region.VeinCount >= 0 ? region.VeinCount :
                    previous.Select(d => d.Id.Substring(0, d.Id.LastIndexOf('-'))).Distinct().Count();
                deposits.RemoveAll(d => d.Id.StartsWith(prefix, StringComparison.Ordinal));
                // 保留侧巢及其改形，矿物重排不改变洞穴几何；伴巢重新设计使用独立的全图内容操作。
                var occupied = new HashSet<int>(baseline.Deposits.Where(d => !d.Id.StartsWith(prefix, StringComparison.Ordinal))
                    .SelectMany(d => d.Cells).Select(c => -c.V * W + c.U));
                foreach (var nest in nests)
                    for (int y = nest.Row - 3; y <= nest.Row + 1; y++)
                        for (int x = nest.U; x <= nest.U + 5; x++) occupied.Add(y * W + x);
                var random = new TerrainRandom(settings.Seed + "|" + minerals.ContentKey + "|region-v2|" + region.Region + "|" + region.Seed + "|" + region.Revision);
                var sites = new List<int>();
                for (int y = 50; y < H - 10; y++) for (int x = 9; x < W - 18; x++)
                {
                    int index = y * W + x;
                    if (cells[index - 1] == 0 && cells[index] != 0 && cells[index] != 8 &&
                        !protection[index] && CaveRuleContent.Nearest(plan, x, y) == region.Region)
                        sites.Add(index);
                }
                for (int i = sites.Count - 1; i > 0; i--)
                {
                    int j = (int)(random.Next() * (i + 1)), saved = sites[i];
                    sites[i] = sites[j]; sites[j] = saved;
                }
                int placed = 0;
                for (int slot = 0; slot < budget; slot++)
                {
                    List<int> footprint = null;
                    for (int trial = 0; trial < 64 && sites.Count > 0; trial++)
                    {
                        int index = sites[sites.Count - 1]; sites.RemoveAt(sites.Count - 1);
                        var shape = (TerrainVeinShape)(int)(random.Next() * 4);
                        footprint = CaveRuleContent.Footprint(cells, protection, occupied, index % W, index / W,
                            shape, slot, true, region.ExposedShare < 0 ? minerals.ExposedShare : region.ExposedShare);
                        footprint.RemoveAll(i => CaveRuleContent.Nearest(plan, i % W, i / W) != region.Region);
                        double share = region.ExposedShare < 0 ? minerals.ExposedShare : region.ExposedShare;
                        int inside = footprint.Count(i => cells[i] != 0);
                        footprint = footprint.Where(i => cells[i] != 0).Concat(footprint.Where(i => cells[i] == 0)
                            .Take((int)(inside * share / (1 - share)))).ToList();
                        if (footprint.Count >= 8) break;
                        footprint = null;
                    }
                    if (footprint == null) continue;
                    int capacity = Math.Max(footprint.Count, region.Capacity < 0 ? minerals.Capacity : region.Capacity);
                    string rarity = random.Next() < .25 ? "rare" : "common";
                    for (int start = 0; start < footprint.Count; start += 64)
                    {
                        var chunk = new List<TerrainMineralCell>();
                        for (int k = start; k < Math.Min(start + 64, footprint.Count); k++)
                        {
                            int index = footprint[k]; occupied.Add(index);
                            chunk.Add(new TerrainMineralCell(index % W, -(index / W),
                                capacity / footprint.Count + (k < capacity % footprint.Count ? 1 : 0)));
                        }
                        var first = chunk[0];
                        deposits.Add(new TerrainDepositBlueprint(prefix + "local" + slot + "-" + start / 64,
                            "r" + region.Region + ":local", first.U, -first.V, rarity, chunk.Sum(c => c.Capacity), chunk));
                    }
                    placed++;
                }
                report?.Invoke("R" + region.Region + "局部重排：" + placed + "/" + budget +
                    "矿团；保持空间、侧巢和其他区域，修订" + region.Revision + "。");
            }
            return new PlayableTerrain(source.WorldId, source.Seed, cells, protection, source.CopySoftRock(),
                source.Rooms.ToArray(), deposits.ToArray(), baseline.CopyShapes(), true, baseline.Background, nests: nests.ToArray());
        }
    }
}
