using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>主角采矿与本地网格共用的纯坐标合同；逻辑格中心与权威坡形碰撞一致，不读取地图或引擎。</summary>
    public static class TerrainMiningGeometry
    {
        public const ushort SoftRockFlag = 32;
        public static int CellU(float x) => (int)Math.Floor(x / PlayableTerrain.CellPixels + .5f);
        public static int CellV(float height) => -(int)Math.Floor((PlayableTerrain.OriginY - height) / PlayableTerrain.CellPixels + .5f);
        public static float CenterX(int u) => u * PlayableTerrain.CellPixels;
        public static float CenterHeight(int v) => PlayableTerrain.OriginY + v * PlayableTerrain.CellPixels;
        /// <summary>按手部到目标格边缘的最短距离检查工具触及范围；角色碰撞不能接近格中心，不扩大配置距离。</summary>
        public static bool WithinReach(float x, float height, int u, int v, float reach)
        {
            float halfCell = PlayableTerrain.CellPixels * .5f;
            float deltaX = Math.Max(0, Math.Abs(CenterX(u) - x) - halfCell);
            float deltaHeight = Math.Max(0, Math.Abs(CenterHeight(v) - height) - halfCell);
            return deltaX * deltaX + deltaHeight * deltaHeight <= reach * reach;
        }
    }
}
