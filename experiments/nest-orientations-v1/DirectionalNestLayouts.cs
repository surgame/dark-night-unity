using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DarkNights.Experiments.NestOrientations
{
    /// <summary>
    /// 六种有限情境的制作配方；顶部与低处采用真实图内分别选取的位置，其余保留旧壁面作对照。
    /// 配方只声明局部格、预置矿群和动作意图，必须再由校验器核对材料、封闭及路径。
    /// </summary>
    public static class DirectionalNestLayouts
    {
        public static DirectionalNestCase Create(string id, JsonElement nearWall)
        {
            if (id == "roof") return Roof();
            if (id == "low") return Low();
            var source = nearWall.GetProperty("variants").EnumerateArray().Single(v => v.GetProperty("id").GetString() == "bulge");
            var c = new DirectionalNestCase { Id = id, Name = "侧壁钻出", Mode = "side", Relation = "矿旁薄壳巢",
                MotionLabel = "沿破口平向接近", Head = nearWall.GetProperty("head").GetInt32(),
                Ore = source.GetProperty("cells").EnumerateArray().Select(v => v.GetProperty("index").GetInt32()).ToArray(),
                Order = source.GetProperty("excavation").EnumerateArray().Select(v => v.GetInt32()).ToArray() };
            c.Pocket = Map(c, new[] { (7,-1),(6,0),(7,0),(8,0),(6,1),(7,1),(8,1) }, true);
            c.Shell = Map(c, new[] { (4,0),(5,0),(5,1) }, true);
            c.Floor = Map(c, new[] { (6,2),(7,2),(8,2) }, true);
            c.Spawn = c.Cell(-7,0); c.Anchor = c.Cell(-7,1); c.ActorStart = c.Head + 1;
            c.ActorEnd = c.Order[4]; c.Goal = c.Order[5];
            c.Bounds = new[] { c.Head % 320 - 11, c.Head / 320 - 5, 19, 13 };
            if (id == "turn" || id == "coexist")
            {
                c.Name = "斜壁转出"; c.Mode = "turn"; c.MotionLabel = "沿错层通道下坡转出";
                var pocket = new List<int>();
                for (int u = 7; u <= 9; u++) for (int v = -3; v <= -1; v++) pocket.Add(c.Cell(-u,v));
                pocket.AddRange(Map(c, new[] { (6,-1),(6,0),(5,0) }, true)); c.Pocket = pocket.ToArray();
                c.Shell = Map(c, new[] { (4,0),(5,1),(6,1) }, true);
                c.Floor = Map(c, new[] { (7,0),(8,0),(9,0) }, true);
                c.Spawn = c.Cell(-8,-2); c.Anchor = c.Cell(-8,-1);
                if (id == "coexist")
                {
                    c.Name = "巢旁多团矿"; c.Mode = "coexist"; c.Relation = "先预留巢区，再安排三团矿";
                    var ore = new HashSet<int>(c.Ore);
                    for (int u = 12; u <= 14; u++) for (int v = 0; v <= 2; v++) ore.Add(c.Cell(-u,v));
                    for (int u = 8; u <= 10; u++) for (int v = 5; v <= 7; v++) ore.Add(c.Cell(-u,v));
                    c.Ore = ore.OrderBy(i => i).ToArray(); c.Bounds = new[] { c.Head % 320 - 17, c.Head / 320 - 5, 25, 15 };
                }
            }
            if (id == "none")
            {
                c.Name = "无巢富矿"; c.Mode = "none"; c.Relation = "保留完整富矿，不附加巢穴"; c.MotionLabel = "采完也不会生成怪物";
                c.HasNest = false; c.Pocket = Array.Empty<int>(); c.Shell = Array.Empty<int>(); c.Floor = Array.Empty<int>();
            }
            c.Allocate(); return c;
        }

        private static DirectionalNestCase Roof()
        {
            var c = new DirectionalNestCase { Id = "roof", Name = "顶部落下", Mode = "fall", Relation = "顶层矿群旁的预置巢腔",
                MotionLabel = "向下加速，落地后沿地面接近", Head = 61 * 320 + 108, Stock = 60 };
            var ore = new List<int>();
            for (int y = -3; y <= 0; y++)
                for (int x = y <= -2 ? -4 : y == -1 ? -4 : -3; x <= (y <= -2 ? -2 : 0); x++) ore.Add(c.Cell(x,y));
            c.Ore = ore.ToArray(); c.Order = Map(c, new[] { (0,0),(-1,0),(-1,-1),(-2,-1),(-2,-2),(0,-1) });
            c.Shell = Map(c, new[] { (0,-2),(1,-2),(1,-1) });
            c.Pocket = Map(c, new[] { (0,-4),(1,-4),(2,-4),(0,-3),(1,-3),(2,-3) });
            c.Floor = Map(c, new[] { (2,-2) }); c.Spawn = c.Cell(0,-3); c.Anchor = c.Cell(1,-3);
            c.Landing = c.Cell(0,3); c.ActorStart = c.Cell(2,3); c.ActorEnd = c.ActorStart; c.Goal = c.Cell(1,3);
            c.Bounds = new[] { 100, 55, 19, 14 }; c.Allocate(); return c;
        }

        private static DirectionalNestCase Low()
        {
            var c = new DirectionalNestCase { Id = "low", Name = "低处沿坡爬出", Mode = "ramp", Relation = "脚下矿层旁，预留短坡出口",
                MotionLabel = "从低处沿连续阶坡爬到采掘面", Head = 115 * 320 + 201 };
            var ore = new List<int>();
            for (int y = 0; y <= 3; y++)
                for (int x = y == 2 ? -5 : -4; x <= (y == 0 ? 0 : y == 1 ? -1 : -2); x++) ore.Add(c.Cell(x,y));
            c.Ore = ore.ToArray(); c.Order = Map(c, new[] { (-3,0),(-3,1),(-2,1),(-1,1),(-1,0),(0,0) });
            c.Shell = Map(c, new[] { (0,1),(1,0),(2,0),(1,1) });
            var pocket = new List<int>();
            for (int x = 2; x <= 6; x++)
                for (int y = x == 2 ? 1 : x == 3 ? 1 : x == 4 ? 2 : 3; y <= (x == 2 ? 2 : x == 3 ? 3 : 4); y++) pocket.Add(c.Cell(x,y));
            c.Pocket = pocket.ToArray(); c.Floor = Map(c, new[] { (4,5),(5,5),(6,5) });
            c.Spawn = c.Cell(5,4); c.Anchor = c.Cell(5,4); c.ActorStart = c.Cell(-3,-1); c.ActorEnd = c.Cell(-2,1); c.Goal = c.Cell(-1,1);
            c.Bounds = new[] { 193, 110, 20, 14 }; c.Allocate(); return c;
        }

        private static int[] Map(DirectionalNestCase c, IEnumerable<(int X, int Y)> values, bool inwardLeft = false)
        {
            return values.Select(v => c.Cell(inwardLeft ? -v.X : v.X, v.Y)).ToArray();
        }
    }
}
