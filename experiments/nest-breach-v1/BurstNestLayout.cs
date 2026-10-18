using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Experiments.NestBreach
{
    /// <summary>
    /// 短颈鼓包旁的薄壳巢穴离线布局；独占构造本批冻结材料、坡形和格级演示路径。
    /// 命中第六矿格时只带开三格预标记薄壳，矿量不被范围破坏吞掉；不代表角色身体通行。
    /// </summary>
    public sealed class BurstNestLayout
    {
        private readonly byte[] materials;
        private readonly bool[] protection;
        private readonly HashSet<int> otherMinerals;
        public int Head { get; }
        public int TriggerStep => 6;
        public int TriggerCell => MiningOrder[TriggerStep - 1];
        public int[] MiningOrder { get; }
        public int[] Ore { get; }
        public int[] Amounts { get; }
        public int[] Shell { get; }
        public int[] Pocket { get; }
        public int[] Floor { get; }
        public int Spawn { get; }
        public int NestAnchor { get; }
        public int[] Retreat { get; }
        public int[] GrubExit { get; }
        public int[] GrubIdle { get; }
        public int Stock => Amounts.Sum();
        public int Remaining => Stock - MiningOrder.Take(TriggerStep).Sum(i => Amounts[Array.IndexOf(Ore, i)]);
        public byte[] ClosedShapes { get; }
        public byte[] BurstShapes { get; }

        public BurstNestLayout(JsonElement source, PlayableTerrain terrain)
        {
            materials = terrain.CopyMaterials(); protection = terrain.CopyProtection(); Head = source.GetProperty("head").GetInt32();
            var variant = source.GetProperty("variants").EnumerateArray().Single(v => v.GetProperty("id").GetString() == "bulge");
            Ore = variant.GetProperty("cells").EnumerateArray().Select(c => c.GetProperty("index").GetInt32()).ToArray();
            Amounts = variant.GetProperty("cells").EnumerateArray().Select(c => c.GetProperty("amount").GetInt32()).ToArray();
            MiningOrder = variant.GetProperty("excavation").EnumerateArray().Select(c => c.GetInt32()).ToArray();
            string replaced = source.GetProperty("replacedVein").GetString();
            otherMinerals = new HashSet<int>(source.GetProperty("mining").GetProperty("veins").EnumerateArray()
                .Where(v => v.GetProperty("id").GetString() != replaced)
                .SelectMany(v => v.GetProperty("cells").EnumerateArray().Select(c => c.GetInt32())));
            Shell = new[] { Map(4, 0), Map(5, 0), Map(5, 1) };
            Pocket = new[] { Map(7, -1), Map(6, 0), Map(7, 0), Map(8, 0), Map(6, 1), Map(7, 1), Map(8, 1) };
            Floor = new[] { Map(6, 2), Map(7, 2), Map(8, 2) };
            Spawn = Map(7, 0); NestAnchor = Map(7, 1);
            if (Ore.Concat(Shell).Concat(Pocket).Concat(Floor).Any(i => !Rock(i)))
                throw new InvalidOperationException("矿体／脆壳／巢腔／地板与真实空腔、保护或其他矿冲突。");
            if (Shell.Intersect(Ore.Concat(Pocket).Concat(Floor)).Any() || Pocket.Intersect(Ore.Concat(Floor)).Any())
                throw new InvalidOperationException("破壳预留与矿／巢腔／地板重叠。");
            if (Pocket.Any(i => Neighbors(i).Any(n => materials[n] == 0 || Ore.Contains(n) || otherMinerals.Contains(n))))
                throw new InvalidOperationException("薄壳巢腔必须初始封闭。");
            if (!Neighbors(TriggerCell).Any(Shell.Contains) || Remaining * 3 < Stock * 2)
                throw new InvalidOperationException("触发必须贴壳，且留至少三分之二储量供风险选择。");
            byte[] closed = (byte[])materials.Clone(); foreach (int index in Pocket) closed[index] = 0;
            ClosedShapes = TerrainShapeGeometry.Build(closed, protection, 320, 192);
            byte[] mined = (byte[])closed.Clone(); foreach (int index in MiningOrder.Take(TriggerStep)) mined[index] = 0;
            if (Path(Spawn, Head + 1, mined) != null) throw new InvalidOperationException("未破壳就已经接通巢穴。");
            byte[] burst = (byte[])mined.Clone(); foreach (int index in Shell) burst[index] = 0;
            BurstShapes = TerrainShapeGeometry.Build(burst, protection, 320, 192);
            GrubExit = Path(Spawn, Head, burst) ?? throw new InvalidOperationException("破壳后没有出巢路径。");
            GrubIdle = Path(Spawn, TriggerCell, burst) ?? throw new InvalidOperationException("没有贴近开采面的路径。");
            Retreat = Path(MiningOrder[TriggerStep - 2], Head + 4, burst) ?? throw new InvalidOperationException("没有格级后退演示路径。");
            if (!Shell.All(i => ReachableShell(i, mined, new HashSet<int>(Shell))))
                throw new InvalidOperationException("脆壳补丁未连到本次击穿开口。");
        }

        public int Map(int u, int v) => (Head / 320 + v) * 320 + Head % 320 - u;
        private bool Rock(int i) => i >= 0 && i < materials.Length && materials[i] == 2 && !protection[i] && !otherMinerals.Contains(i);

        private static bool ReachableShell(int target, byte[] mined, HashSet<int> shell)
        {
            byte[] opened = (byte[])mined.Clone(); foreach (int index in shell) opened[index] = 0;
            return shell.Any(i => Neighbors(i).Any(n => mined[n] == 0)) && Path(target, shell.First(), opened) != null;
        }

        private static int[] Path(int start, int end, byte[] map)
        {
            if (map[start] != 0 || map[end] != 0) return null;
            var queue = new Queue<int>(); var parents = new Dictionary<int, int> { [start] = -1 }; queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == end)
                {
                    var result = new List<int>();
                    for (int i = end; i >= 0; i = parents[i]) result.Add(i);
                    result.Reverse(); return result.ToArray();
                }
                foreach (int next in Neighbors(current))
                    if (map[next] == 0 && !parents.ContainsKey(next)) { parents[next] = current; queue.Enqueue(next); }
            }
            return null;
        }

        public static IEnumerable<int> Neighbors(int i)
        {
            if (i % 320 > 0) yield return i - 1;
            if (i % 320 < 319) yield return i + 1;
            if (i >= 320) yield return i - 320;
            if (i < 61120) yield return i + 320;
        }
    }
}
