using System;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>
    /// 现有固定船体的安全停泊几何合同；运行与作者检查共用原脚点、船壳及坡道净空数值。
    /// 只读查询，不选择新泊位、不塑形、不修改船体或人物状态；自然地表不满足时明确拒绝。
    /// </summary>
    public static class ShipLandingGeometry
    {
        public static bool HullClear(Func<float, float, bool> solid, float x, float height)
        {
            for (float px = -ShipGeometry.HalfWidth; px <= ShipGeometry.HalfWidth; px += 2)
                for (float py = 1; py <= ShipGeometry.Roof; py += 2)
                    if (ShipGeometry.Hull(px, py) && solid(x + px, height + py)) return false;
            return true;
        }

        public static bool Supported(Func<float, float, bool> solid, float x, float height)
        {
            if (!solid(x - 56, height - 1) || !solid(x + 104, height - 1) ||
                !solid(x + ShipGeometry.RampToe - 8, height - 1)) return false;
            for (float local = ShipGeometry.RampToe - HeroControlDefinition.BodyHalfWidth;
                local <= ShipGeometry.RampHinge + HeroControlDefinition.BodyHalfWidth; local += 4)
                for (float head = 1; head <= HeroControlDefinition.BodyHeight + 1; head += 4)
                    if (solid(x + local, height + ShipGeometry.Floor(Math.Max(ShipGeometry.RampToe, local)) + head)) return false;
            return true;
        }

        /// <summary>普通地面开局在配置泊位高度直接停泊；按同份最终坡形检查，不补接地距离或跳过出舱空间。</summary>
        public static string Inspect(PlayableTerrain terrain, PlanetDefinition planet)
        {
            if (terrain == null || planet == null) return "缺少当前完整地图或泊位配置。";
            byte[] cells = terrain.CopyMaterials(), shapes = terrain.CopyShapes();
            bool Solid(float x, float height)
            {
                int u = (int)Math.Floor(x / PlayableTerrain.CellPixels + .5);
                int row = (int)Math.Floor((PlayableTerrain.OriginY - height) / PlayableTerrain.CellPixels + .5);
                if (u < 0 || u >= TerrainGenerationSettings.Width || row >= TerrainGenerationSettings.Height) return true;
                if (row < 0) return false;
                int index = row * TerrainGenerationSettings.Width + u;
                return cells[index] != 0 && TerrainShapeGeometry.Contains((TerrainCellShape)shapes[index],
                    x / PlayableTerrain.CellPixels - u + .5f,
                    (height - PlayableTerrain.OriginY) / PlayableTerrain.CellPixels + row + .5f);
            }
            if (!Supported(Solid, planet.DockX, planet.DockHeight))
                return "当前固定泊位缺少两脚／坡道脚支撑，或出舱净空被地形占用；地图未填平或挪动泊位。";
            return HullClear(Solid, planet.DockX, planet.DockHeight) ? "" :
                "当前固定泊位的船体包络与最终地形相交；地图未挖空船体位置。";
        }
    }
}
