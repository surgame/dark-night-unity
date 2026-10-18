using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Experiments.NestOrientations
{
    /// <summary>
    /// 本批离线情境的真实材料、预留和格级连通准入；构造既有矿床并以Core重建坡形。
    /// 只确认冻结格级路径与下落列，不把路径、贴图或回放当身体／采掘距离／运动能力验收。
    /// </summary>
    public sealed class DirectionalNestValidation
    {
        private readonly byte[] materials;
        private readonly bool[] protection;
        private readonly HashSet<int> otherOre;

        public DirectionalNestValidation(PlayableTerrain terrain, IEnumerable<int> otherOre)
        {
            materials = terrain.CopyMaterials(); protection = terrain.CopyProtection(); this.otherOre = new HashSet<int>(otherOre);
        }

        public void Validate(DirectionalNestCase c)
        {
            bool Rock(int i) => i >= 0 && i < materials.Length && materials[i] == 2 && !protection[i] && !otherOre.Contains(i);
            var ore = new HashSet<int>(c.Ore); var pocket = new HashSet<int>(c.Pocket); var shell = new HashSet<int>(c.Shell);
            if (c.Ore.Concat(c.Pocket).Concat(c.Shell).Concat(c.Floor).Any(i => !Rock(i))) throw new InvalidOperationException(c.Id + "真实围岩占格冲突。");
            if (pocket.Overlaps(ore) || shell.Overlaps(ore) || shell.Overlaps(pocket) || c.Floor.Any(i => pocket.Contains(i) || shell.Contains(i) || ore.Contains(i)))
                throw new InvalidOperationException(c.Id + "矿／壳／巢腔／地板重叠。");
            if (pocket.Any(i => Neighbors(i).Any(n => materials[n] == 0 || ore.Contains(n) || otherOre.Contains(n))))
                throw new InvalidOperationException(c.Id + "巢腔初始未封闭。");
            if (c.HasNest && !Neighbors(c.Order[5]).Any(shell.Contains)) throw new InvalidOperationException(c.Id + "触发不贴壳。");
            byte[] closed = (byte[])materials.Clone(); foreach (int i in pocket) closed[i] = 0;
            c.ClosedShapes = Runs(TerrainShapeGeometry.Build(closed, protection, 320, 192));
            foreach (int i in c.Order.Take(6))
            {
                if (!ore.Contains(i) || !Neighbors(i).Any(n => closed[n] == 0)) throw new InvalidOperationException(c.Id + "冻结开挖序不接空格。");
                closed[i] = 0;
            }
            if (closed[c.ActorStart] != 0 || closed[c.ActorEnd] != 0) throw new InvalidOperationException(c.Id + "作业标记落在实岩。");
            c.ActorPath = Path(c.ActorStart, c.ActorEnd, closed) ?? throw new InvalidOperationException(c.Id + "无格级作业路径。");
            if (c.HasNest && Path(c.Spawn, c.ActorEnd, closed) != null) throw new InvalidOperationException(c.Id + "破壳前已经通巢。");
            foreach (int i in shell) closed[i] = 0;
            c.BurstShapes = Runs(TerrainShapeGeometry.Build(closed, protection, 320, 192));
            if (c.HasNest)
            {
                if (!pocket.Contains(c.Spawn)) throw new InvalidOperationException(c.Id + "出生未预藏巢腔。");
                c.ExitPath = Path(c.Mode == "fall" ? c.Landing : c.Spawn, c.Goal, closed) ?? throw new InvalidOperationException(c.Id + "破壳后没有出巢路径。");
                if (c.Mode == "fall")
                {
                    if (c.Spawn % 320 != c.Landing % 320 || c.Spawn >= c.Landing) throw new InvalidOperationException("顶部下落轴无效。");
                    for (int i = c.Spawn; i <= c.Landing; i += 320) if (closed[i] != 0) throw new InvalidOperationException("下落列被岩阻断。");
                    if (closed[c.Landing + 320] == 0) throw new InvalidOperationException("顶部落点无支撑。");
                }
            }
            var remaining = new HashSet<int>(c.Ore); var groups = new List<int>(); int beds = 0;
            while (remaining.Count > 0)
            {
                int first = remaining.First(); var queue = new Queue<int>(); var group = new List<int>(); remaining.Remove(first); queue.Enqueue(first);
                while (queue.Count > 0)
                {
                    int at = queue.Dequeue(); group.Add(at);
                    foreach (int next in Neighbors(at)) if (remaining.Remove(next)) queue.Enqueue(next);
                }
                groups.Add(group.Count);
                for (int offset = 0; offset < group.Count; offset += 64)
                {
                    var cells = group.Skip(offset).Take(64).Select(i => new TerrainMineralCell(i % 320, -i / 320, c.Amounts[Array.IndexOf(c.Ore,i)])).ToArray();
                    _ = new TerrainDepositBlueprint("orientation-" + c.Id + "-" + beds++, "gallery", c.Head % 320, c.Head / 320, "common", cells.Sum(v => v.Capacity), cells);
                }
            }
            c.Groups = groups.OrderBy(n => n).ToArray(); c.BlueprintCount = beds;
            if (c.Amounts.Sum() != c.Stock || c.Amounts.Any(n => n <= 0) || c.HasNest && c.Remaining <= c.Stock / 2)
                throw new InvalidOperationException(c.Id + "矿量守恒或剩余风险收益条件失败。");
        }

        public static IEnumerable<int> Neighbors(int i)
        {
            if (i % 320 > 0) yield return i - 1;
            if (i % 320 < 319) yield return i + 1;
            if (i >= 320) yield return i - 320;
            if (i < 61120) yield return i + 320;
        }

        private static int[] Path(int start, int end, byte[] map)
        {
            if (map[start] != 0 || map[end] != 0) return null;
            var queue = new Queue<int>(); var parents = new Dictionary<int,int> { [start] = -1 }; queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                if (at == end)
                {
                    var path = new List<int>(); for (int i = end; i >= 0; i = parents[i]) path.Add(i); path.Reverse(); return path.ToArray();
                }
                foreach (int next in Neighbors(at)) if (map[next] == 0 && !parents.ContainsKey(next)) { parents[next] = at; queue.Enqueue(next); }
            }
            return null;
        }

        public static string Runs(IEnumerable<byte> values)
        {
            var result = new List<string>(); int last = -1, count = 0;
            foreach (byte value in values)
            {
                if (count > 0 && value != last) { result.Add(last + ":" + count); count = 0; }
                last = value; count++;
            }
            if (count > 0) result.Add(last + ":" + count); return string.Join(",", result);
        }
    }
}
