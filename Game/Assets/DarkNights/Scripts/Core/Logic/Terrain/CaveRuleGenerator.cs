using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>规则模式基础空间生成入口；仅消费空间随机流，内容重抽不会改变本层。围岩／盲端允许保留，最终形状从格子统一派生。</summary>
    public static class CaveRuleGenerator
    {
        public static TerrainBlueprint Generate(TerrainGenerationSettings settings)
        {
            var s = settings.CopyValidated(); var plan = CaveRulePlanner.Build(s);
            const int w = TerrainGenerationSettings.Width, h = TerrainGenerationSettings.Height;
            var cells = new byte[w * h]; var protection = new bool[cells.Length];
            var surface = CaveExplorationGenerator.Surface(s);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                protection[i] = x < 3 || x >= w - 3 || y >= h - 3;
                cells[i] = protection[i] ? (byte)8 : y < surface[x] ? (byte)0 : y < surface[x] + 3 ? (byte)1 : (byte)2;
            }
            for (int n = 0; n < plan.Rooms.Count; n++) CaveRuleGeometry.Room(cells, protection, plan.Rooms[n], n);
            foreach (var edge in plan.Connections)
            {
                var a = plan.Rooms[edge.From]; var b = plan.Rooms[edge.To];
                // 侧接口偏置，保留中央岩柱／隔层；端点不代表人物能进出的承诺。
                int ax = a.X + Math.Sign(edge.BendX - a.X) * a.Width / 4;
                int bx = b.X + Math.Sign(edge.BendX - b.X) * b.Width / 4;
                CaveRuleGeometry.Corridor(cells, protection, ax, a.Y, edge.BendX, edge.BendY, edge.Radius, edge.From * .73, edge.CoverLength);
                CaveRuleGeometry.Corridor(cells, protection, edge.BendX, edge.BendY, bx, b.Y, edge.Radius, edge.To * .73);
            }
            CaveRuleEntrance.Carve(cells, protection, s.Rules, plan.Rooms[0], 61, surface[61]);
            return new TerrainBlueprint(s, cells, protection, surface, plan.Rooms.ToArray(), new bool[cells.Length],
                Array.Empty<TerrainDepositBlueprint>(), plan.Connections.ToArray(), TerrainShapeGeometry.Build(cells, protection, w, h));
        }
    }
}
