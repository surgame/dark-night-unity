using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>九入口模板的天然地形表达；天空和泊位阶段后使用同一冻结规则重建，根系与巨石只塑形，不附带植物／滚石或通行修复。</summary>
    public static class CaveRuleEntrance
    {
        public static void Carve(byte[] cells, bool[] protection, CaveRuleSettings rules, TerrainRoom target, int startX, int surface)
        {
            int x = Math.Clamp(startX, 8, 290), y = Math.Clamp(surface, 8, 160);
            int bendX = (x + target.X) / 2, bendY = y + 12;
            if (rules.Entrance == "shaft") { bendX = x; bendY = target.Y; }
            else if (rules.Entrance == "hillside" || rules.Entrance == "boulder") { bendX = target.X - 15; bendY = y + 20; }
            else if (rules.Entrance == "fault-step") { bendX = x + 17; bendY = y + 5; }
            else if (rules.Entrance == "riverbed") { bendX = x + 25; bendY = y + 7; }
            CaveRuleGeometry.Corridor(cells, protection, x, y - 4, bendX, bendY, rules.Entrance == "shaft" ? 3 : 5);
            CaveRuleGeometry.Corridor(cells, protection, bendX, bendY, target.X, target.Y, 4, 1.2);
            if (rules.Entrance == "hillside")
            {
                // 侧向横洞：地表缺口在左，横洞覆岩保持完整，不用斜槽替代洞口。
                for (int dx = -14; dx <= 10; dx++) for (int dy = -5; dy <= 23; dy++)
                    CaveRuleGeometry.Set(cells, protection, x + dx, y + dy, 0);
                CaveRuleGeometry.Corridor(cells, protection, x + 10, y + 19, target.X, y + 19, 4);
                for (int dx = 13; dx <= target.X - x + 4; dx++) for (int dy = 0; dy < 13; dy++)
                    CaveRuleGeometry.Set(cells, protection, x + dx, y + dy, 2);
            }
            if (rules.Entrance == "valley" || rules.Entrance == "riverbed")
                for (int dx = -14; dx <= 14; dx++)
                    for (int dy = -6; dy < (rules.Entrance == "valley" ? 18 - Math.Abs(dx) : 5); dy++)
                        CaveRuleGeometry.Set(cells, protection, x + dx, y + dy, 0);
            if (rules.Entrance == "collapse")
            {
                CaveRuleGeometry.Disk(cells, protection, x + 10, y + 8, 11, 0);
                for (int n = 0; n < 5; n++) CaveRuleGeometry.Disk(cells, protection, x + n * 4 + 1, y + 13 + n % 2, 2.6, 2);
            }
            if (rules.Entrance == "riverbed")
                for (int dx = -19; dx <= 31; dx++) for (int dy = -5; dy <= 5 + Math.Max(0, dx) / 10; dy++)
                    CaveRuleGeometry.Set(cells, protection, x + dx, y + dy, 0);
            if (rules.Entrance == "bridge")
            {
                CaveRuleGeometry.Disk(cells, protection, x + 14, y + 11, 17, 0);
                for (int dx = -3; dx <= 31; dx++)
                    for (int dy = 0; dy <= 3; dy++) CaveRuleGeometry.Set(cells, protection, x + dx, y + dy, 2);
            }
            if (rules.Entrance == "boulder")
            {
                CaveRuleGeometry.Disk(cells, protection, x + 17, y + 12, 16, 0);
                CaveRuleGeometry.Disk(cells, protection, x + 19, y + 3, 12, 2);
            }
            if (rules.Entrance == "fault-step")
                for (int dx = -10; dx < 31; dx++) for (int dy = -4; dy <= 5 + (dx + 10) / 10 * 5; dy++)
                    CaveRuleGeometry.Set(cells, protection, x + dx, y + dy, 0);
            if (rules.Entrance == "roots")
                for (int n = 0; n < 4; n++)
                    CaveRuleGeometry.Corridor(cells, protection, x + n * 10 - 12, y - 3, bendX + n * 3 - 4, bendY + 9, 2, n);
        }
    }
}
