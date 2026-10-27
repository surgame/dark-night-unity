using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>
    /// 连通洞室各入口独立声明的空腔、岩体与地下接口；不预挖共同通道，不使用固定地表起点。
    /// 只写本次基础地图，保护格保持；不附带通行修复、植物或滚石行为。
    /// </summary>
    public static class CaveRuleEntrance
    {
        public static void Carve(byte[] cells, bool[] protection, CaveLayoutSettings rules, TerrainRoom target, int[] surface,
            bool disableValleySculpt = false)
        {
            switch (rules.Entrance)
            {
                case "valley": Valley(cells, protection, target, surface, disableValleySculpt); break;
                case "collapse": Collapse(cells, protection, target, surface); break;
                case "hillside": Hillside(cells, protection, target, surface); break;
                case "bridge": Bridge(cells, protection, target, surface); break;
                case "riverbed": Riverbed(cells, protection, target, surface); break;
                case "boulder": Boulder(cells, protection, target, surface); break;
                case "fault-step": FaultStep(cells, protection, target, surface); break;
                case "roots": Roots(cells, protection, target, surface); break;
                case "shaft": Shaft(cells, protection, target, surface); break;
                default: throw new ArgumentException("未知入口类型，不能回退为共同通道。");
            }
        }

        private static void Valley(byte[] cells, bool[] protection, TerrainRoom room, int[] surface, bool disabled)
        {
            int x = Math.Clamp(room.Left - 18, 18, 275), y = surface[x];
            if (!disabled) CaveRuleGeometry.Polygon(cells, protection, x, y, new[]
            {
                -12, -6, 12, -6, 12, 1, 10, 6, 6, 10, 0, 12, -5, 10, -9, 6, -12, 0
            }, 0);
            // 凹谷从右侧壁进入覆岩下方，不把整个谷底削成通向首室的三角斜槽。
            CaveRuleGeometry.Corridor(cells, protection, x + 8, y + 7, x + 22, y + 10, 3, .4);
            CaveRuleGeometry.Corridor(cells, protection, x + 22, y + 10, room.X, room.Y, 3, 2.3);
        }

        private static void Collapse(byte[] cells, bool[] protection, TerrainRoom room, int[] surface)
        {
            int x = Math.Clamp(room.Left - 7, 18, 276), y = surface[x];
            CaveRuleGeometry.Polygon(cells, protection, x, y, new[]
            {
                -15, -9, 17, -9, 20, 4, 16, 13, 6, 20, -4, 17, -13, 8
            }, 0);
            CaveRuleGeometry.Corridor(cells, protection, x + 13, y + 15, room.X, room.Y, 3, 2.1);
            CaveRuleGeometry.Disk(cells, protection, x - 7, y + 12, 3.5, 2);
            CaveRuleGeometry.Disk(cells, protection, x + 3, y + 18, 3, 2);
            CaveRuleGeometry.Disk(cells, protection, x + 13, y + 11, 2.5, 2);
        }

        private static void Hillside(byte[] cells, bool[] protection, TerrainRoom room, int[] surface)
        {
            int x = Math.Clamp(room.Left - 23, 18, 270), y = surface[x];
            // 外部地面转入侧向洞檐；上下轮廓分别声明，保留原有覆岩，不后填矩形顶盖。
            CaveRuleGeometry.Polygon(cells, protection, x, y, new[]
            {
                -14, -8, 0, -8, 5, 2, 13, 3, 23, 8, 35, 17,
                34, 32, 23, 24, 12, 14, 3, 6, -7, 1
            }, 0);
            CaveRuleGeometry.Corridor(cells, protection, x + 32, y + 24, room.X, room.Y, 4, 1.4);
        }

        private static void Bridge(byte[] cells, bool[] protection, TerrainRoom room, int[] surface)
        {
            int x = Math.Clamp(room.X - 16, 22, 275), y = surface[x];
            CaveRuleGeometry.Disk(cells, protection, x, y + 13, 19, 0);
            CaveRuleGeometry.Corridor(cells, protection, x + 13, y + 22, room.X + 8, room.Y, 4, 3.2);
            CaveRuleGeometry.Polygon(cells, protection, x, y, new[]
            {
                -21, 0, -10, -1, 2, 1, 13, 0, 21, 3, 19, 7, 8, 5, -4, 4, -18, 5
            }, 2);
        }

        private static void Riverbed(byte[] cells, bool[] protection, TerrainRoom room, int[] surface)
        {
            int x = Math.Clamp(room.Left - 30, 22, 260), y = surface[x];
            CaveRuleGeometry.Polygon(cells, protection, x, y, new[]
            {
                -20, -7, 38, -7, 41, 5, 33, 9, 20, 10, 5, 6, -10, 5
            }, 0);
            CaveRuleGeometry.Polygon(cells, protection, x, y, new[]
            {
                27, 6, 38, 7, 46, 13, 49, 23, 44, 29, 38, 18, 31, 13
            }, 0);
            CaveRuleGeometry.Corridor(cells, protection, x + 44, y + 24, room.X, room.Y, 3, 4.3);
        }

        private static void Boulder(byte[] cells, bool[] protection, TerrainRoom room, int[] surface)
        {
            int x = Math.Clamp(room.Left - 9, 22, 275), y = surface[x];
            CaveRuleGeometry.Polygon(cells, protection, x, y, new[]
            {
                -17, -6, 22, -6, 27, 11, 24, 23, 12, 25, -4, 16, -15, 7
            }, 0);
            CaveRuleGeometry.Corridor(cells, protection, x + 21, y + 20, room.X, room.Y, 3, 5.1);
            CaveRuleGeometry.Polygon(cells, protection, x, y, new[]
            {
                -4, -4, 8, -7, 22, -2, 26, 6, 18, 12, 3, 10, -6, 4
            }, 2);
        }

        private static void FaultStep(byte[] cells, bool[] protection, TerrainRoom room, int[] surface)
        {
            int x = Math.Clamp(room.Left - 24, 18, 266), y = surface[x];
            CaveRuleGeometry.Polygon(cells, protection, x, y, new[]
            {
                -9, -6, 9, -6, 9, 2, 21, 2, 21, 10, 33, 10, 33, 20,
                44, 20, 44, 29, 31, 29, 31, 21, 19, 21, 19, 13, 6, 13, 6, 5, -9, 5
            }, 0);
            CaveRuleGeometry.Corridor(cells, protection, x + 39, y + 25, room.X, room.Y, 3, 6.2);
        }

        private static void Roots(byte[] cells, bool[] protection, TerrainRoom room, int[] surface)
        {
            int x = Math.Clamp(room.X - 13, 24, 275), y = surface[x];
            CaveRuleGeometry.Corridor(cells, protection, x - 12, surface[x - 12] - 2, x - 2, y + 17, 2, .8);
            CaveRuleGeometry.Corridor(cells, protection, x + 10, surface[x + 10] - 2, x + 7, y + 12, 2, 2.4);
            CaveRuleGeometry.Corridor(cells, protection, x + 7, y + 12, x - 2, y + 24, 2, 4.7);
            CaveRuleGeometry.Corridor(cells, protection, x - 2, y + 17, room.X - 5, room.Y, 2, 3.6);
        }

        private static void Shaft(byte[] cells, bool[] protection, TerrainRoom room, int[] surface)
        {
            int x = Math.Clamp(room.X + room.Width / 4, 10, 300), y = surface[x];
            CaveRuleGeometry.Polygon(cells, protection, x, y, new[]
            {
                -4, -6, 4, -6, 3, room.Y - y + 3, -3, room.Y - y + 3
            }, 0);
            CaveRuleGeometry.Corridor(cells, protection, x, room.Y, room.X, room.Y, 3, 7.2);
        }
    }
}
