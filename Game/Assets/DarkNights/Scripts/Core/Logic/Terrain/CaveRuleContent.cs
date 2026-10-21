using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>从同批基础空间规划区域矿量与侧巢；全图样点洗牌后按区域轮转，内容流不移动洞室。所有局部补丁从基础重建，失败有界报告。</summary>
    public static class CaveRuleContent
    {
        private const int W = TerrainGenerationSettings.Width, H = TerrainGenerationSettings.Height;

        public static PlayableTerrain Generate(PlayableTerrain source, TerrainGenerationSettings settings, Action<string> report = null)
        {
            if (source == null || !source.Expedition || source.GeologySites.Count != 0 || source.Nests.Count != 0 ||
                source.Deposits.Count != 0 || source.Minerals != null || source.Damage.Count != 0)
                throw new ArgumentException("规则内容只接受未运行、无内容补丁的基础空间。");
            var rules = settings.CopyValidated().Rules; var plan = CaveRulePlanner.Build(settings);
            var random = new TerrainRandom(rules.ContentKey);
            byte[] cells = source.CopyMaterials(); bool[] protection = source.CopyProtection();
            var occupied = new HashSet<int>(); var reserved = new HashSet<int>();
            var deposits = new List<TerrainDepositBlueprint>(); var nests = new List<TerrainNest>();
            var regions = Enumerable.Range(0, plan.Rooms.Count).ToArray(); Shuffle(regions, random);
            var empty = new bool[regions.Length]; var rich = new bool[regions.Length];
            foreach (int region in regions)
            {
                empty[region] = region != 0 && random.Next() < rules.EmptyShare;
                rich[region] = region != 0 && random.Next() < rules.RichShare;
            }
            var sites = new List<int>[regions.Length];
            for (int i = 0; i < sites.Length; i++) sites[i] = new List<int>();
            for (int y = 50; y < H - 9; y++) for (int x = 9; x < W - 18; x++)
                if (cells[y * W + x - 1] == 0 && Rock(cells, protection, x, y) && Rock(cells, protection, x + 1, y))
                {
                    int region = Nearest(plan, x, y);
                    if (!empty[region]) sites[region].Add(y * W + x);
                }
            for (int i = 0; i < sites.Length; i++)
            {
                var values = sites[i].ToArray(); Shuffle(values, random); sites[i] = new List<int>(values);
            }
            double maximum = Math.Max(1, plan.Distances.Max());
            int placed = 0, attempted = 0;
            for (int round = 0; round < rules.VeinCount && placed < rules.VeinCount; round++)
                foreach (int region in regions)
                {
                    if (placed >= rules.VeinCount) break;
                    if (sites[region].Count == 0) continue;
                    double depth = plan.Distances[region] / maximum;
                    bool isRich = rich[region], nest = rules.Nests && isRich && random.Next() < .55 + depth * .35;
                    bool exposed = random.Next() < .75;
                    int capacity = (int)(rules.Capacity * (isRich ? 1.25 + depth : .45 + depth * .6));
                    string rarity = region != 0 && random.Next() < depth * (isRich ? .85 : .35) ? "rare" : "common";
                    int selected = -1; List<int> footprint = null;
                    int trials = Math.Min(64, sites[region].Count);
                    for (int trial = 0; trial < trials; trial++)
                    {
                        int index = sites[region][sites[region].Count - 1]; sites[region].RemoveAt(sites[region].Count - 1); attempted++;
                        if (reserved.Contains(index) || nest && !FitsNest(cells, protection, reserved, index % W, index / W)) continue;
                        var shape = (TerrainVeinShape)(int)(random.Next() * 4);
                        footprint = Footprint(cells, protection, occupied, index % W, index / W, shape, round, exposed, rules.ExposedShare);
                        if (footprint.Count < 8) continue;
                        selected = index; break;
                    }
                    if (selected < 0)
                    {
                        report?.Invoke("区域" + region + "：本轮围岩／矿层／巢壳空间不足，跳过" + (nest ? "伴巢富矿" : "矿团") + "。");
                        continue;
                    }
                    string purpose = isRich ? "rich" : exposed ? "clue" : "sparse";
                    capacity = Math.Max(capacity, footprint.Count);
                    int each = Math.Max(1, capacity / footprint.Count), remainder = Math.Max(0, capacity - each * footprint.Count);
                    for (int start = 0; start < footprint.Count; start += 64)
                    {
                        var chunk = new List<TerrainMineralCell>();
                        for (int k = start; k < Math.Min(start + 64, footprint.Count); k++)
                        {
                            int index = footprint[k]; occupied.Add(index);
                            chunk.Add(new TerrainMineralCell(index % W, -(index / W), each + (k < remainder ? 1 : 0)));
                        }
                        var first = chunk[0];
                        deposits.Add(new TerrainDepositBlueprint("rule-r" + region + "-v" + placed + "-" + start / 64,
                            "r" + region + ":" + purpose, first.U, -first.V, rarity, chunk.Sum(c => c.Capacity), chunk));
                    }
                    if (nest)
                    {
                        var site = new TerrainNest(nests.Count + 1, selected % W + 3, selected / W);
                        for (int y = site.Row - 3; y <= site.Row + 1; y++) for (int x = site.U; x <= site.U + 5; x++) reserved.Add(y * W + x);
                        for (int y = site.Row - 2; y <= site.Row; y++) for (int x = site.U + 1; x <= site.U + 4; x++) cells[y * W + x] = 0;
                        nests.Add(site);
                    }
                    report?.Invoke("区域" + region + " · " + purpose + " · 声明路程" + (int)plan.Distances[region] +
                        " · 支线层" + plan.BranchLevels[region] + " · " + rarity + " · " + footprint.Count + "格／" + capacity + "储量" + (nest ? " · 侧巢" : ""));
                    placed++;
                }
            foreach (int region in regions.Where(i => empty[i])) report?.Invoke("区域" + region + "：规划空腔／无矿支路。");
            foreach (int region in regions)
            {
                var ore = deposits.Where(d => d.Id.StartsWith("rule-r" + region + "-")).ToArray();
                int visible = 0, hidden = 0, rare = 0;
                foreach (var deposit in ore) foreach (var cell in deposit.Cells)
                {
                    if (cells[-cell.V * W + cell.U] == 0) visible += cell.Capacity; else hidden += cell.Capacity;
                    if (deposit.Rarity == "rare") rare += cell.Capacity;
                }
                report?.Invoke("R" + region + "实际：露矿" + visible + "／壁内" + hidden + "／金矿" + rare + "储量。");
            }
            report?.Invoke("内容完成 " + placed + "/" + rules.VeinCount + "矿团；候选尝试" + attempted + "；侧巢" + nests.Count + "。");
            var shapes = TerrainShapeGeometry.Build(cells, protection, W, H);
            var background = new BackgroundBakeDescriptor(source.WorldId, source.Seed, cells, shapes);
            return new PlayableTerrain(source.WorldId, source.Seed, cells, protection, source.CopySoftRock(), source.Rooms.ToArray(),
                deposits.ToArray(), shapes, true, background, nests: nests.ToArray());
        }

        private static List<int> Footprint(byte[] cells, bool[] protection, HashSet<int> occupied, int x, int y,
            TerrainVeinShape shape, int variation, bool exposed, double exposedShare)
        {
            var inside = new List<int>(); var outside = new List<int>();
            for (int dy = 0; dy < TerrainVeinStudyLayout.Height; dy++) for (int dx = 0; dx < TerrainVeinStudyLayout.Width; dx++)
            {
                int wx = x + dx - 4, wy = y + dy - 8, index = wy * W + wx;
                if (!TerrainVeinShapeMask.Contains(shape, dx, dy, variation) || protection[index] || cells[index] == 8 || occupied.Contains(index)) continue;
                if (cells[index] == 0) outside.Add(index); else inside.Add(index);
            }
            // 露头与壁内来自同一形状；空腔矿格比例受输入限制，不能透过保护格补矿。
            if (exposed) inside.AddRange(outside.Take(Math.Max(1, (int)(inside.Count * exposedShare / (1 - exposedShare)))));
            return inside;
        }

        private static bool FitsNest(byte[] cells, bool[] protection, HashSet<int> reserved, int x, int y)
        {
            for (int dy = -3; dy <= 1; dy++) for (int dx = 3; dx <= 8; dx++)
                if (!Rock(cells, protection, x + dx, y + dy) || reserved.Contains((y + dy) * W + x + dx)) return false;
            return true;
        }

        private static bool Rock(byte[] cells, bool[] protection, int x, int y) => cells[y * W + x] != 0 && cells[y * W + x] != 8 && !protection[y * W + x];
        private static int Nearest(CaveRulePlan plan, int x, int y)
        {
            int best = 0; double distance = double.MaxValue;
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                var room = plan.Rooms[i]; double dx = x - room.X, dy = y - room.Y;
                double candidate = dx * dx + dy * dy;
                if (candidate < distance) { best = i; distance = candidate; }
            }
            return best;
        }

        private static void Shuffle(int[] values, TerrainRandom random)
        {
            for (int i = values.Length - 1; i > 0; i--)
            { int j = (int)(random.Next() * (i + 1)), value = values[i]; values[i] = values[j]; values[j] = value; }
        }
    }
}
