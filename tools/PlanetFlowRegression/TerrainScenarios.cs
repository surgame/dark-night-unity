using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Tools.PlanetFlowRegression
{
    /// <summary>以多种子和多泊位检验最终材料、保护面、矿床及坡形净空；格子步道检查不冒充 Runtime 主角移动验收。</summary>
    internal static class TerrainScenarios
    {
        private const int W = TerrainGenerationSettings.Width, H = TerrainGenerationSettings.Height;
        private const string World = "00000000000000000000000000000011";

        public static void Run(ScenarioReport r)
        {
            var planets = new[]
            {
                new PlanetDefinition("default", "默认"),
                new PlanetDefinition("high", "高平台", dockRow: 28, maximumLift: 256),
                new PlanetDefinition("low", "低平台", dockRow: 48, arrivalHeight: 256, maximumLift: 560),
                new PlanetDefinition("middle", "中央平台", dockColumn: 160),
                new PlanetDefinition("right", "右侧平台", dockColumn: 284),
                new PlanetDefinition("wide", "宽平台", dockColumn: 200, landingWidth: 64)
            };
            for (int preset = 0; preset < planets.Length; preset++)
            {
                var planet = planets[preset];
                for (int n = 0; n < (preset == 0 ? 100 : 12); n++)
                {
                    string seed = "FLOW-PURE-" + n;
                    r.Run(planet.Id + "/" + seed, () => CheckMap(r, planet, seed));
                }
            }
            var space = PlanetTerrainGenerator.Space(World);
            r.Check(space.Expedition && space.Deposits.Count == 0 && space.Rooms.Count == 0, "太空载体没有星球矿床或洞室");
            r.Check(space.CopyMaterials().Take((H - 1) * W).All(v => v == 0) &&
                space.CopyMaterials().Skip((H - 1) * W).All(v => v == 8), "太空载体仅底边为基岩");
            r.Check(space.CopyProtection().Count(v => v) == W && space.CopyShapes().All(v => v == 0), "太空载体保护与坡形合法");
            r.Check(space.Background.CopyMaterials().SequenceEqual(space.CopyMaterials()), "太空背景参考与地图相同");
            r.Reject(() => PlanetTerrainGenerator.Space("invalid"), "太空载体拒绝非法世界身份");
            r.Reject(() => PlanetTerrainGenerator.Generate(planets[0], "", World), "星球生成拒绝空实际种子");
            r.Reject(() => PlanetTerrainGenerator.Generate(null, "seed", World), "星球生成拒绝空预设");
            int callbacks = 0;
            try
            {
                PlanetTerrainGenerator.Generate(planets[0], "CANCEL", World, () => ++callbacks > 5);
                r.Check(false, "取消应在逐行构建期间中止");
            }
            catch (OperationCanceledException) { r.Check(callbacks == 6, "取消在逐行构建期间中止"); }
            var immutable = PlanetTerrainGenerator.Generate(planets[0], "IMMUTABLE", World);
            byte[] copied = immutable.CopyMaterials(); byte original = copied[W * 40 + 36]; copied[W * 40 + 36] = 8;
            r.Check(immutable.Material(36, 40) == original, "生成结果不能经返回数组修改");
            r.Check(immutable.Background.CopyMaterials()[W * 40 + 36] == original, "背景参考不能经地图数组修改");
        }

        private static void CheckMap(ScenarioReport r, PlanetDefinition planet, string seed)
        {
            var map = PlanetTerrainGenerator.Generate(planet, seed, World);
            string label = planet.Id + "/" + seed;
            var cells = map.CopyMaterials(); var protection = map.CopyProtection();
            var shapes = map.CopyShapes(); var soft = map.CopySoftRock();
            r.Check(map.Count == W * H && map.Expedition && map.WorldId == World && map.Seed == seed, label + "身份尺寸");
            r.Check(cells.Take(planet.DockRow * W).All(v => v == 0) &&
                protection.Take(planet.DockRow * W).All(v => !v), label + "开放天空");
            r.Check(cells.Skip((H - 1) * W).All(v => v == 8), label + "保留底边基岩");
            int left = planet.DockColumn - planet.LandingWidth / 2;
            bool pad = true, legal = true;
            for (int y = planet.DockRow; y < planet.DockRow + 3; y++)
                for (int x = left; x < left + planet.LandingWidth; x++)
                    pad &= cells[y * W + x] == 1 && protection[y * W + x] && shapes[y * W + x] == 0 && !soft[y * W + x];
            for (int i = 0; i < cells.Length; i++)
                legal &= cells[i] <= 8 && shapes[i] <= 12 && (cells[i] != 0 || shapes[i] == 0) &&
                    (!protection[i] || cells[i] != 0 && !soft[i] && shapes[i] == 0);
            r.Check(pad, label + "完整保护平台"); r.Check(legal, label + "材料坡形保护合同");
            r.Check(map.Deposits.Count > 0 && map.Deposits.Select(d => d.Id).Distinct().Count() == map.Deposits.Count &&
                map.Deposits.All(d => d.Y >= planet.DockRow + 3 && !protection[d.Y * W + d.X]), label + "地表无残留矿床");
            r.Check(cells.SequenceEqual(map.Background.CopyMaterials()) && shapes.SequenceEqual(map.Background.CopyShapes()), label + "背景为最终地形");
            var repeat = PlanetTerrainGenerator.Generate(planet, seed, World);
            r.Check(repeat.CopyMaterials().SequenceEqual(cells) && repeat.Background.ReferenceHash == map.Background.ReferenceHash,
                label + "固定种子完全可复现");
            r.Check(HasWalkingCells(map, planet), label + "三格净高地面步道连接洞室");
            r.Check(TerrainWalkProbe.ReachesRoom(map, planet), label + "两单位步幅真实坡形连续地面通行");
            bool hull = true;
            foreach (float height in new[] { planet.DockHeight, planet.DockHeight + planet.ArrivalHeight, planet.DockHeight + planet.MaximumLift })
                for (float x = -ShipGeometry.HalfWidth; x <= ShipGeometry.HalfWidth; x += 4)
                    for (float y = 1; y <= ShipGeometry.Roof; y += 4)
                        if (ShipGeometry.Hull(x, y)) hull &= !Solid(map, shapes, planet.DockX + x, height + y);
            r.Check(hull && Solid(map, shapes, planet.DockX - 56, planet.DockHeight - 1) &&
                Solid(map, shapes, planet.DockX + 104, planet.DockHeight - 1), label + "泊位到达高空船壳净空及双起落架支撑");
        }

        private static bool HasWalkingCells(PlayableTerrain map, PlanetDefinition planet)
        {
            var seen = new HashSet<int>(); var todo = new Queue<int>();
            int start = planet.DockRow * W + planet.DockColumn;
            seen.Add(start); todo.Enqueue(start);
            while (todo.Count != 0)
            {
                int item = todo.Dequeue(), x = item % W, row = item / W;
                if (row >= planet.DockRow + 8 && map.Rooms.Any(room => x >= room.Left + 2 && x <= room.Left + room.Width - 2 &&
                    row >= room.Top + 3 && row <= room.Top + room.Height + 3)) return true;
                foreach (int dx in new[] { -1, 1 })
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = x + dx, ny = row + dy;
                        if (nx < 3 || nx >= W - 3 || ny < 3 || ny >= H - 3) continue;
                        int next = ny * W + nx;
                        if (seen.Contains(next) || map.Material(nx, ny) == 0 ||
                            map.Material(nx, ny - 1) != 0 || map.Material(nx, ny - 2) != 0 || map.Material(nx, ny - 3) != 0) continue;
                        seen.Add(next); todo.Enqueue(next);
                    }
            }
            return false;
        }

        internal static void Diagnose(string seed)
        {
            var planet = new PlanetDefinition("default", "默认");
            var map = PlanetTerrainGenerator.Generate(planet, seed, World);
            var source = CaveExplorationGenerator.Generate(new TerrainGenerationSettings
                { Seed = seed, ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile });
            foreach (var room in source.Rooms)
            {
                int floor = room.Y;
                while (floor < H - 5 && source.MaterialAt(room.X, floor) == 0) floor++;
                Console.WriteLine($"room {room.X},{room.Y} size={room.Width},{room.Height} floor={floor}");
            }
            for (int row = 35; row < 100; row++)
            {
                string line = "";
                for (int x = 16; x < 155; x++) line += map.Rooms.Any(room => room.X == x && room.Y == row) ? "R" : map.Material(x, row) == 0 ? "." : "#";
                Console.WriteLine(row.ToString("D3") + " " + line);
            }
        }

        private static bool Solid(PlayableTerrain map, byte[] shapes, float x, float height)
        {
            int u = (int)Math.Floor(x / 16 + .5), row = (int)Math.Floor((PlayableTerrain.OriginY - height) / 16 + .5);
            if (u < 0 || u >= W || row >= H) return true;
            if (row < 0 || map.Material(u, row) == 0) return false;
            return TerrainShapeGeometry.Contains((TerrainCellShape)shapes[row * W + u],
                x / 16 - u + .5f, (height - PlayableTerrain.OriginY) / 16 + row + .5f);
        }
    }
}
