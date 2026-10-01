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
        /// <summary>将瞄准向量归一化；零向量或非有限输入不形成采集射线。</summary>
        public static bool Direction(float x, float height, out float dx, out float dh)
        {
            dx = dh = 0;
            double length = Math.Sqrt((double)x * x + (double)height * height);
            if (double.IsNaN(length) || double.IsInfinity(length) || length < .0001) return false;
            dx = (float)(x / length); dh = (float)(height / length);
            return true;
        }

        /// <summary>有限单位方向射线与真实格形的首次交点；矩形边界和坡形半平面共同裁剪，不将斜坡空白当作墙。</summary>
        public static bool RayCell(float x, float height, float dx, float dh, float reach,
            int u, int v, TerrainCellShape shape, out float distance)
        {
            float half = PlayableTerrain.CellPixels * .5f;
            float left = CenterX(u) - half, bottom = CenterHeight(v) - half;
            float entry = 0, exit = reach;
            distance = 0;
            if (!Clip(left - x, -dx, ref entry, ref exit) ||
                !Clip(x - left - PlayableTerrain.CellPixels, dx, ref entry, ref exit) ||
                !Clip(bottom - height, -dh, ref entry, ref exit) ||
                !Clip(height - bottom - PlayableTerrain.CellPixels, dh, ref entry, ref exit)) return false;
            if (shape != TerrainCellShape.Full)
            {
                float edge = TerrainShapeGeometry.Edge(shape, 0);
                float slope = TerrainShapeGeometry.Edge(shape, 1) - edge;
                float value = height - bottom - slope * (x - left) - edge * PlayableTerrain.CellPixels;
                float velocity = dh - slope * dx;
                if (TerrainShapeGeometry.Ceiling(shape)) { value = -value; velocity = -velocity; }
                if (!Clip(value, velocity, ref entry, ref exit)) return false;
            }
            distance = entry;
            return entry <= exit && exit >= 0 && entry <= reach;
        }

        // 将 value + velocity * t <= 0 裁剪到当前有限参数区间。
        private static bool Clip(float value, float velocity, ref float entry, ref float exit)
        {
            if (Math.Abs(velocity) < .000001f) return value <= 0;
            float crossing = -value / velocity;
            if (velocity > 0) exit = Math.Min(exit, crossing);
            else entry = Math.Max(entry, crossing);
            return entry <= exit;
        }
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
