using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Experiments.NestOrientations
{
    /// <summary>
    /// 本批单个局部设计情境的导出数据；构造阶段由布局器独占写入，随后序列化冻结。
    /// 所有格索引指向真实320×192地图，出巢动作与重力分开描述，不创建运行业务状态。
    /// </summary>
    public sealed class DirectionalNestCase
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Mode { get; set; }
        public string Relation { get; set; }
        public string MotionLabel { get; set; }
        public int Head { get; set; }
        public bool HasNest { get; set; } = true;
        public int[] Ore { get; set; }
        public int[] Amounts { get; set; }
        public int Stock { get; set; } = 64;
        public int[] Order { get; set; }
        public int[] Shell { get; set; } = Array.Empty<int>();
        public int[] Pocket { get; set; } = Array.Empty<int>();
        public int[] Floor { get; set; } = Array.Empty<int>();
        public int Spawn { get; set; }
        public int Anchor { get; set; }
        public int ActorStart { get; set; }
        public int ActorEnd { get; set; }
        public int Goal { get; set; }
        public int Landing { get; set; } = -1;
        public int[] ExitPath { get; set; } = Array.Empty<int>();
        public int[] ActorPath { get; set; } = Array.Empty<int>();
        public int[] Groups { get; set; } = Array.Empty<int>();
        public int Remaining { get; set; }
        public int BlueprintCount { get; set; }
        public string ClosedShapes { get; set; }
        public string BurstShapes { get; set; }
        public int[] Bounds { get; set; }

        public int Cell(int x, int y) => (Head / 320 + y) * 320 + Head % 320 + x;

        public void Allocate()
        {
            var ore = new HashSet<int>(Ore); var first = Order.Where(ore.Contains).ToList();
            first.AddRange(Ore.Where(i => !first.Contains(i)));
            var values = new Dictionary<int, int>();
            for (int n = 0; n < first.Count; n++)
                values[first[n]] = n < 2 ? 2 : (Stock - 4) / (first.Count - 2) +
                    (n - 2 < (Stock - 4) % (first.Count - 2) ? 1 : 0);
            Amounts = Ore.Select(i => values[i]).ToArray();
            Remaining = Stock - Order.Take(6).Sum(i => values[i]);
        }
    }
}
