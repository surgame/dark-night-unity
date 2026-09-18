using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>地形破坏的纯规则表；不读取网络、玩家或地图状态，运行时由权威地图组合这些规则。</summary>
    public static class TerrainDestructionPolicy
    {
        public const int MaximumTargets = 13;
        private static readonly TerrainOffset[] HandMineOffsets = { new TerrainOffset(0, 0) };
        private static readonly TerrainOffset[] ExplosiveOffsets =
        {
            new TerrainOffset(0, 0), new TerrainOffset(-1, -1), new TerrainOffset(0, -1), new TerrainOffset(1, -1),
            new TerrainOffset(-1, 0), new TerrainOffset(1, 0), new TerrainOffset(-1, 1), new TerrainOffset(0, 1),
            new TerrainOffset(1, 1), new TerrainOffset(-2, 0), new TerrainOffset(2, 0),
            new TerrainOffset(0, -2), new TerrainOffset(0, 2)
        };

        /// <summary>爆破作用范围中的相对格坐标；返回只读数组，调用者不得改变规则表。</summary>
        public static IReadOnlyList<TerrainOffset> Offsets(TerrainEditAction action)
        {
            switch (action)
            {
                case TerrainEditAction.HandMine: return HandMineOffsets;
                case TerrainEditAction.Explosive: return ExplosiveOffsets;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        /// <summary>判断单格是否能被指定动作清除；保护位和基岩优先于任何工具。</summary>
        public static bool CanDestroy(TerrainEditAction action, byte material, bool protectedCell, bool softRock)
        {
            if (protectedCell || material == 0 || material == 8) return false;
            switch (action)
            {
                case TerrainEditAction.HandMine: return softRock || material == 4 || material == 5 || material == 6;
                case TerrainEditAction.Explosive: return true;
                default: return false;
            }
        }
    }

}
