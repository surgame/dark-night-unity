using System;
using System.Collections.Generic;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>共同候选中的人工塌方空间校验；读取最终材料、坡形、保护、矿层及巢位，失败返回具体原因，不改形或修路。运行占位仍由GeologyLanding在提交点复核。</summary>
    public static class GeologySiteValidation
    {
        public static string Check(PlayableTerrain map, TerrainGeologySite site)
        {
            try
            {
                var config = GeologySiteConfiguration.Resolve(site);
                var protectedCells = map.CopyProtection(); var shapes = map.CopyShapes();
                var softRock = map.CopySoftRock();
                var ore = new HashSet<CellCoord>(map.Deposits.SelectMany(d => d.Cells).Select(c => new CellCoord(c.U, c.V)));
                var sources = new HashSet<CellCoord>(Enumerable.Range(0, config.RockCount).Select(config.Source));
                var landings = new HashSet<CellCoord>(Enumerable.Range(0, config.RubbleCount).Select(config.Landing));
                foreach (var cell in sources.Concat(landings))
                {
                    if (!Inside(cell, site.AllowedU, site.AllowedV, site.AllowedWidth, site.AllowedHeight)) return "源岩或瓦砾超出允许改形范围";
                    if (!Known(cell)) return "源岩或落点越界";
                    int index = -cell.V * TerrainGenerationSettings.Width + cell.U;
                    if (protectedCells[index] || ore.Contains(cell)) return "源岩或落点覆盖保护格／矿格";
                    if (shapes[index] != 0) return "首片不支持源岩／落点斜坡或非完整格形状";
                    if (sources.Contains(cell) && softRock[index]) return "源岩带软岩标记，首片仅接受无附加标记的完整slate";
                    if (sources.Contains(cell) ? map.Material(cell.U, -cell.V) != 2 : !Empty(cell)) return "源岩须为完整slate，落点须为空格";
                    foreach (var nest in map.Nests)
                        if (cell.U >= nest.U - 3 && cell.U <= nest.U + 4 && cell.V >= -nest.Row && cell.V <= -nest.Row + 2)
                            return "塌方范围侵入巢腔、封壳或出虫净空";
                }
                foreach (var cell in landings)
                {
                    var below = new CellCoord(cell.U, cell.V - 1);
                    if (!landings.Contains(below) && (!Known(below) || Empty(below) || shapes[-below.V * TerrainGenerationSettings.Width + below.U] != 0))
                        return "落点缺少完整平地支撑；不自动填平斜坡";
                }
                if (!GeologyLanding.PathClear(config, cell => sources.Contains(cell) || Empty(cell))) return "冻结落石轨迹碰到现有地形／坡形";
                for (int u = site.RetreatU; u < site.RetreatU + site.RetreatWidth; u++)
                {
                    for (int v = site.RetreatV; v < site.RetreatV + site.RetreatHeight; v++)
                        if (!Empty(new CellCoord(u, v)) || landings.Contains(new CellCoord(u, v))) return "退避区缺少初始身体净空或被落地瓦砾覆盖";
                    var floor = new CellCoord(u, site.RetreatV - 1);
                    if (!Known(floor) || Empty(floor) || sources.Contains(floor) || shapes[-floor.V * TerrainGenerationSettings.Width + u] != 0)
                        return "退避区缺少保留的完整地面";
                }
                if (!config.EnterRegion && (!Known(config.Support) || Empty(config.Support) || ore.Contains(config.Support) ||
                    sources.Contains(config.Support) || protectedCells[-config.Support.V * TerrainGenerationSettings.Width + config.Support.U]))
                    return "支撑触发格须为源岩外独立、可挖且无矿的岩格";
                if (!Known(new CellCoord(config.RegionU, config.RegionV)) ||
                    !Known(new CellCoord(config.RegionU + config.RegionWidth - 1, config.RegionV + config.RegionHeight - 1))) return "触发区越界";
                if (config.EnterRegion && !Standing(config.RegionU, config.RegionV, config.RegionWidth, config.RegionHeight)) return "进入区缺少三格宽、三格高的初始站位";
                return "";

                bool Known(CellCoord c) => c.U >= 0 && c.U < TerrainGenerationSettings.Width && c.V <= 0 && c.V > -TerrainGenerationSettings.Height;
                bool Empty(CellCoord c) => Known(c) && map.Material(c.U, -c.V) == 0;
                bool Standing(int u, int v, int width, int height)
                {
                    for (int x = u; x <= u + width - 3; x++) for (int y = v; y <= v + height - 3; y++)
                        if (Enumerable.Range(0, 3).All(dx => Enumerable.Range(0, 3).All(dy => Empty(new CellCoord(x + dx, y + dy))) &&
                            Known(new CellCoord(x + dx, y - 1)) && !Empty(new CellCoord(x + dx, y - 1)))) return true;
                    return false;
                }
            }
            catch (ArgumentException error) { return error.Message; }
            catch (FormatException error) { return error.Message; }
        }

        private static bool Inside(CellCoord cell, int u, int v, int width, int height) =>
            cell.U >= u && cell.U < u + width && cell.V >= v && cell.V < v + height;
    }
}
