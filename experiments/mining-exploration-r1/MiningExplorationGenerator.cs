using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Experiments.MiningExploration
{
    /// <summary>
    /// R1离线矿脉规划器，消费正式生成的同一地形，以空腔距离、壁厚、露头条件及洞室用途选矿。
    /// 原作者图和正式配置不被改写；产出已有矿床合同，可在审阅后接入正式生成路径。
    /// </summary>
    public sealed class MiningExplorationGenerator
    {
        private readonly MiningRouteField field;
        private readonly MineMapDefinition map;
        private readonly string seed;
        private readonly MiningVeinGrowth growth;
        private readonly List<(int Head, int Wall, uint Rank)> candidates;
        private readonly List<string> diagnostics = new List<string>();
        public IReadOnlyList<string> Diagnostics => diagnostics.AsReadOnly();

        public MiningExplorationGenerator(MiningRouteField field, MineMapDefinition map, string seed)
        {
            this.field = field; this.map = map; this.seed = seed;
            growth = new MiningVeinGrowth(field, seed);
            candidates = new List<(int, int, uint)>();
            for (int i = 320 * 3; i < 320 * 189; i++)
            {
                if (field.Distance(i) < 8 || field.Materials[i] != 0 || field.Owner(i) == 0) continue;
                // 露头靠近支撑面；距离只供分档，不把这一筛选宣称为角色可达。
                if (field.Materials[i + 320] == 0 && field.Materials[i + 640] == 0) continue;
                foreach (int wall in MiningRouteField.Neighbors(i))
                    if (field.IsWall(wall) && wall / 320 >= i / 320)
                        candidates.Add((i, wall, TerrainRandom.Hash(seed + "|site|" + i + "|" + wall)));
            }
            candidates.Sort((a, b) => a.Rank.CompareTo(b.Rank));
        }

        public MiningVein[] Generate()
        {
            var veins = new List<MiningVein>();
            var occupied = new HashSet<int>();
            int[] wallTargets = { 3, 5, 7 }, roadTargets = { 16, 14, 12 };
            for (int tier = 0; tier < 3; tier++)
            {
                Place(veins, occupied, tier, true, wallTargets[tier]);
                Place(veins, occupied, tier, false, roadTargets[tier]);
            }
            int[] budgets = { 90, 180, 360 };
            for (int tier = 0; tier < 3; tier++)
            {
                var indices = Enumerable.Range(0, veins.Count).Where(i => veins[i].Tier == tier).ToArray();
                int totalCells = indices.Sum(i => veins[i].Cells.Count);
                int extra = Math.Max(0, budgets[tier] - totalCells);
                double totalWeight = indices.Sum(i => veins[i].Cells.Count * (veins[i].Interior ? 1.7 : .7));
                int allocated = 0;
                foreach (int index in indices)
                {
                    var vein = veins[index];
                    int share = (int)Math.Floor(extra * vein.Cells.Count * (vein.Interior ? 1.7 : .7) / totalWeight);
                    veins[index] = vein.WithCapacity(vein.Cells.Count + share); allocated += share;
                }
                for (int n = allocated; n < extra && indices.Length > 0; n++)
                {
                    int index = indices[(n - allocated) % indices.Length];
                    veins[index] = veins[index].WithCapacity(veins[index].Capacity + 1);
                }
                diagnostics.Add("tier=" + tier + "; planned-reserves=" + budgets[tier] +
                    "; actual-reserves=" + indices.Sum(i => veins[i].Capacity));
            }
            return veins.ToArray();
        }

        private void Place(List<MiningVein> veins, HashSet<int> occupied, int tier, bool interior, int target)
        {
            int placed = 0;
            foreach (var candidate in candidates)
            {
                if (placed >= target) break;
                if (field.Tier(field.Distance(candidate.Head)) != tier || occupied.Contains(candidate.Head)) continue;
                int spacing = interior ? 9 : 4 + (int)(candidate.Rank % 3);
                if (veins.Any(v => SquaredDistance(v.Head, candidate.Head) < spacing * spacing)) continue;
                int count = interior ? 9 + tier * 4 + (int)(candidate.Rank % 9) : 1 + (int)(candidate.Rank % 3);
                var cells = growth.Grow(candidate.Head, candidate.Wall, count, occupied);
                if (interior && cells.Count < 6) continue;
                string kind = Kind(candidate.Head, tier, interior);
                var vein = new MiningVein("r1-vein-" + candidate.Head, candidate.Head,
                    field.Owner(candidate.Head), field.Distance(candidate.Head), tier, interior,
                    kind, cells.Count, cells);
                veins.Add(vein); occupied.UnionWith(cells); placed++;
            }
            diagnostics.Add((interior ? "wall" : "road") + "; tier=" + tier + "; target=" + target + "; actual=" + placed);
        }

        private string Kind(int head, int tier, bool interior)
        {
            double value = TerrainRandom.Noise(TerrainRandom.Hash(seed + "|kind"), head);
            double gold = tier == 0 ? 0 : tier == 1 ? .25 : interior ? .8 : .3;
            return value < gold ? "gold" : "iron";
        }

        public MiningVein[] AssignRoom(MiningVein[] baseline, int room, MiningRoomRole role)
        {
            if (room < 1 || room >= map.Rooms.Length) throw new ArgumentOutOfRangeException(nameof(room));
            if (role == MiningRoomRole.Ordinary) return (MiningVein[])baseline.Clone();
            if (role == MiningRoomRole.Quiet) return baseline.Where(v => v.Room != room).ToArray();
            var result = baseline.Where(v => v.Room != room || !v.Interior).ToList();
            var occupied = new HashSet<int>(result.SelectMany(v => v.Cells));
            int count = 0;
            foreach (var candidate in candidates.Where(c => field.Owner(c.Head) == room))
            {
                if (count >= 2) break;
                if (occupied.Contains(candidate.Head) || result.Any(v => SquaredDistance(v.Head, candidate.Head) < 16)) continue;
                var cells = growth.Grow(candidate.Head, candidate.Wall, 28 + (int)(candidate.Rank % 13), occupied);
                if (cells.Count < 9) continue;
                int distance = field.Distance(candidate.Head), tier = field.Tier(distance);
                int capacity = cells.Count * (2 + tier);
                result.Add(new MiningVein("r1-rich-" + candidate.Head, candidate.Head, room, distance,
                    tier, true, Kind(candidate.Head, tier, true), capacity, cells));
                occupied.UnionWith(cells); count++;
            }
            // 无足够厚岩时保留已有矿脉，不制造穿墙矿体或悄悄挪动洞室。
            if (count == 0) return (MiningVein[])baseline.Clone();
            // 特殊洞室可以重配本地预算，但仍遵守用户确定的近段＜中段＜远段总储量。
            for (int tier = 1; tier >= 0; tier--)
            {
                int limit = result.Where(v => v.Tier == tier + 1).Sum(v => v.Capacity) - 1;
                int excess = result.Where(v => v.Tier == tier).Sum(v => v.Capacity) - limit;
                for (int i = 0; i < result.Count && excess > 0; i++)
                {
                    var vein = result[i];
                    if (vein.Tier != tier || !vein.Id.StartsWith("r1-rich-", StringComparison.Ordinal)) continue;
                    int reduction = Math.Min(excess, vein.Capacity - vein.Cells.Count);
                    result[i] = vein.WithCapacity(vein.Capacity - reduction); excess -= reduction;
                }
                if (excess > 0) return (MiningVein[])baseline.Clone();
            }
            return result.ToArray();
        }

        private static int SquaredDistance(int a, int b)
        {
            int dx = a % 320 - b % 320, dy = a / 320 - b / 320;
            return dx * dx + dy * dy;
        }

        public PlayableTerrain Install(PlayableTerrain source, IEnumerable<MiningVein> veins)
        {
            var deposits = veins.Select(v => v.ToDeposit()).ToArray();
            return new PlayableTerrain(source.WorldId, source.Seed, source.CopyMaterials(), source.CopyProtection(),
                source.CopySoftRock(), source.Rooms.ToArray(), deposits, source.CopyShapes(), true, source.Background);
        }
    }
}
