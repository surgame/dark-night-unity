using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>
    /// 星球表面候选生成器；复用天然洞穴，在冻结前统一铺设开放天空、完整泊位和可步行下洞通路。
    /// 全部输入输出为纯数据，可在后台执行；背景参考必须在最后一次地形修改之后捕获。
    /// </summary>
    public static class PlanetTerrainGenerator
    {
        private const int W = TerrainGenerationSettings.Width;
        private const int H = TerrainGenerationSettings.Height;

        public static PlayableTerrain Generate(PlanetDefinition planet, string seed, string worldId, Func<bool> cancelled = null)
        {
            if (planet == null) throw new ArgumentNullException(nameof(planet));
            CheckCancellation(cancelled);
            var source = CaveExplorationGenerator.Generate(new TerrainGenerationSettings
            { Seed = seed, ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile });
            CheckCancellation(cancelled);
            var cells = source.CopyMaterials();
            var protection = new bool[cells.Length];
            var soft = source.CopySoftRock();
            int left = planet.DockColumn - planet.LandingWidth / 2;
            int right = left + planet.LandingWidth - 1;
            for (int y = 0; y < H; y++)
            {
                CheckCancellation(cancelled);
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    protection[i] = source.IsProtected(x, y) && cells[i] != 0;
                    if (y < planet.DockRow)
                    {
                        cells[i] = 0; protection[i] = false; soft[i] = false;
                    }
                    else if (y < Math.Max(planet.DockRow + 3, source.Surface[x] + 3))
                    {
                        // 地表连续，飞船全部起落架、船壳投影和坡道出口均受平台覆盖。
                        cells[i] = 1; soft[i] = false;
                        protection[i] = x >= left && x <= right && y < planet.DockRow + 3 || x < 3 || x >= W - 3;
                    }
                    if (protection[i]) soft[i] = false;
                }
            }

            TerrainRoom destination = SelectRoom(source, planet, left, right, out int targetFloor);
            int start = destination.X < left ? left - 1 : right + 1;
            CarveWalkway(cells, protection, soft, start, planet.DockRow, destination.X, targetFloor);
            CheckCancellation(cancelled);
            byte[] shapes = TerrainShapeGeometry.Build(cells, protection, W, H);
            var deposits = new List<TerrainDepositBlueprint>();
            foreach (var deposit in source.Deposits)
                if (deposit.Y >= Math.Max(planet.DockRow + 3, source.Surface[deposit.X] + 3) &&
                    !protection[deposit.Y * W + deposit.X])
                    deposits.Add(deposit);
            CheckCancellation(cancelled);
            var background = new BackgroundBakeDescriptor(worldId, seed, cells, shapes);
            return new PlayableTerrain(worldId, seed, cells, protection, soft, source.Rooms.ToArray(),
                deposits.ToArray(), shapes, true, background);
        }

        public static PlayableTerrain Space(string worldId)
        {
            // 初始环境沿用现有 AMP1 地图生命周期；只有协议要求的底边基岩，没有可开采星球。
            // 这不是一颗虚构星球：船舱关闭，地面玩法门控由航程阶段负责。
            const string seed = "SPACE-CARRIER-V1";
            var cells = new byte[W * H];
            var protection = new bool[cells.Length];
            var shapes = new byte[cells.Length];
            for (int x = 0; x < W; x++)
            {
                int i = (H - 1) * W + x;
                cells[i] = 8; protection[i] = true;
            }
            var background = new BackgroundBakeDescriptor(worldId, seed, cells, shapes);
            return new PlayableTerrain(worldId, seed, cells, protection, new bool[cells.Length],
                Array.Empty<TerrainRoom>(), Array.Empty<TerrainDepositBlueprint>(), shapes, true, background);
        }

        private static TerrainRoom SelectRoom(TerrainBlueprint source, PlanetDefinition planet, int left, int right, out int targetFloor)
        {
            TerrainRoom selected = null;
            int best = int.MaxValue;
            targetFloor = 0;
            foreach (var room in source.Rooms)
            {
                int floor = room.Y;
                while (floor < H - 5 && source.MaterialAt(room.X, floor) == 0) floor++;
                int start = room.X < left ? left - 1 : right + 1;
                int horizontal = Math.Abs(room.X - start);
                int vertical = floor - planet.DockRow;
                if (room.X >= left && room.X <= right || vertical < 0 || horizontal < vertical + 2) continue;
                int score = horizontal + vertical * 2;
                if (score >= best) continue;
                best = score; selected = room; targetFloor = floor;
            }
            return selected ?? throw new InvalidOperationException("当前泊位无法以每列最多一格的坡道连接洞室，请调整泊位。");
        }

        private static void CheckCancellation(Func<bool> cancelled)
        {
            if (cancelled != null && cancelled()) throw new OperationCanceledException("星球候选生成已取消。");
        }

        private static void CarveWalkway(byte[] cells, bool[] protection, bool[] soft, int start, int top, int end, int bottom)
        {
            int distance = Math.Abs(end - start), direction = Math.Sign(end - start);
            for (int step = 0; step <= distance; step++)
            {
                int x = start + step * direction;
                int floor = top + (int)Math.Round((bottom - top) * step / (double)distance);
                for (int y = floor - 5; y < floor; y++)
                {
                    int i = y * W + x;
                    cells[i] = 0; protection[i] = false; soft[i] = false;
                }
                for (int y = floor; y < floor + 2; y++)
                {
                    int i = y * W + x;
                    cells[i] = 2; protection[i] = false; soft[i] = false;
                }
            }
        }
    }
}
