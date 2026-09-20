using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>正式远征地图的冻结候选；保留洞穴坡形，仅在飞船泊位铺设局部保护地面。</summary>
    public static class ExpeditionTerrainGenerator
    {
        public const int DockLeft = 24, DockRight = 48, DockRow = 40;
        public const float ShipX = 568;
        public static PlayableTerrain Generate(string seed, string worldId)
        {
            var source = CaveExplorationGenerator.Generate(new TerrainGenerationSettings
            { Seed = seed, ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile });
            var cells = source.CopyMaterials(); var shapes = source.CopyShapes(); var soft = source.CopySoftRock();
            var protection = new bool[cells.Length];
            for (int y = 0; y < source.Height; y++) for (int x = 0; x < source.Width; x++)
            {
                int i = y * source.Width + x; protection[i] = source.IsProtected(x, y) && cells[i] != 0;
                if (x >= DockLeft && x < DockRight && y < DockRow + 3)
                {
                    cells[i] = y < DockRow ? (byte)0 : (byte)1;
                    protection[i] = y >= DockRow; shapes[i] = 0; soft[i] = false;
                }
            }
            // 首个矿房的必经阶坡：每格最多升降一格，净高四格；正式主角和矿工共用碰撞。
            var room = source.Rooms[0];
            for (int x = DockRight; x <= room.X; x++)
            {
                int floor = DockRow + (int)Math.Round((room.Y + 3 - DockRow) * (x - DockRight) / (double)(room.X - DockRight));
                for (int y = floor - 4; y < floor; y++)
                { int i = y * source.Width + x; cells[i] = 0; shapes[i] = 0; protection[i] = false; soft[i] = false; }
                cells[floor * source.Width + x] = 2;
            }
            shapes = TerrainShapeGeometry.Build(cells, protection, source.Width, source.Height);
            var deposits = source.Deposits.ToArray();
            int mineX = DockRight + 5;
            int mineFloor = DockRow + (int)Math.Round((room.Y + 3 - DockRow) * 5 / (double)(room.X - DockRight));
            deposits[0] = new TerrainDepositBlueprint("cave-0", "gallery", mineX, mineFloor - 1, "common", 80);
            return new PlayableTerrain(worldId, seed, cells, protection, soft, source.Rooms.ToArray(), deposits, shapes, true);
        }
    }
}
