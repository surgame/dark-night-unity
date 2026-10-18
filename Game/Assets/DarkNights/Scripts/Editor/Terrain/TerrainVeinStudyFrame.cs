using DarkNights.Core.Config.Terrain;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 冻结制作布局的单帧演示投影；拆墙、采矿、打开封岩分别派生，不回写候选或业务地图。
    /// 时间及决策只控制示意姿势，不代表正式触发窗口、身体碰撞、伤害或AI状态。
    /// </summary>
    internal sealed class TerrainVeinStudyFrame
    {
        internal float Time { get; }
        internal int Decision { get; }
        internal int Columns { get; }
        internal int Mined { get; }
        internal int Harvested { get; }
        internal int RemovedWalls => Columns * 2;
        internal bool Opened => Columns == 4;
        internal bool Creature { get; }
        internal string Phase { get; }

        internal TerrainVeinStudyFrame(TerrainVeinStudyLayout layout, float time, int decision)
        {
            Time = time; Decision = decision;
            Columns = Mathf.Clamp(Mathf.FloorToInt((time - .5f) / .8f), 0, 3);
            if (decision == 1 && time >= 7.5f) Columns = 4;
            Mined = Mathf.Clamp(Mathf.FloorToInt((time - 3.5f) / .55f), 0, 4);
            int harvested = 0;
            for (int i = 0; i < Mined; i++) harvested += layout.Capacity(3 + i, 8);
            Harvested = harvested; Creature = Opened && layout.HasNest;
            Phase = time < 3.5f ? "拆前景墙，背景矿保留" : time < 5.5f ? "采背景矿，前景通道保持" :
                decision == 2 ? "带矿后退，巢口保持封闭" : Creature ? "巢口打开，单虫沿开口接近" :
                Opened ? "无巢富矿，空间已打开" : "薄墙前兆：仍有矿可采，等待选择";
        }

        internal bool Removed(TerrainVeinStudyLayout layout, int x, int y) => layout.Passage(x, y, Columns);
        internal bool Collected(int x, int y) => y == 8 && x >= 3 && x < 3 + Mined;
    }
}
