using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>
    /// 作者关卡矿洞的正式生成分支；将原尺度基线逐行嵌入并增加声明洞段，输出既有冻结蓝图。
    /// 运行时仍只有会话原生前景和矿层；天空、泊位、入口步骤及最终背景捕获由星球流水线继续处理。
    /// </summary>
    public static class MineMapGenerator
    {
        private const int W = TerrainGenerationSettings.Width, H = TerrainGenerationSettings.Height;

        public static TerrainBlueprint Generate(TerrainGenerationSettings input)
        {
            var settings = input.CopyValidated();
            var map = settings.MineMap ?? throw new ArgumentException("缺少作者矿洞输入。");
            byte[] baseline = map.DecodeBase(), cells = new byte[W * H];
            var protection = new bool[cells.Length]; var soft = new bool[cells.Length];
            var surface = new int[W];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                protection[i] = x < 3 || x >= W - 3 || y >= H - 3;
                cells[i] = protection[i] ? (byte)8 : (byte)2;
                surface[x] = 40;
            }
            for (int y = 0; y < map.BaseHeight; y++) for (int x = 0; x < map.BaseWidth; x++)
            {
                int i = Index(map, x, y); byte value = baseline[y * map.BaseWidth + x];
                cells[i] = value == 0 ? (byte)0 : (byte)2;
                soft[i] = value == 2;
            }
            new MineMapCarving(map, cells, soft).Carve();
            RequireBaseline(map, baseline, cells, soft);
            var rooms = map.Rooms.Select(r => new TerrainRoom(r.Id,
                map.OffsetX + r.X, map.OffsetY + r.Floor - 3, r.Radius * 2 + 1, r.Height + 4)).ToArray();
            var deposits = new List<TerrainDepositBlueprint>();
            foreach (var ore in map.BaseOres)
            {
                int i = Index(map, ore.X, ore.Y);
                if (ore.Buried && (!soft[i] || cells[i] == 0))
                    throw new InvalidOperationException("扩展段破坏了基线矿袋覆盖：" + ore.Id);
                deposits.Add(Deposit(map, "base-" + ore.Id, ore.X, ore.Y, ore.Kind, ore.Capacity));
            }
            foreach (var room in map.Rooms.Where(r => !r.Base))
            {
                if (room.SurfaceOre)
                    deposits.Add(Deposit(map, room.Id + "-surface", room.X, room.Floor - 1,
                        room.Floor > 100 ? "gold" : "iron", 3));
                if (!room.BuriedOre) continue;
                int floor = FloorAt(map, cells, room.X, room.Floor, out int column);
                for (int y = floor; y < floor + 3; y++) for (int x = column; x < column + 3; x++)
                {
                    int i = Index(map, x, y);
                    if (cells[i] != 0 && !protection[i]) soft[i] = true;
                }
                deposits.Add(Deposit(map, room.Id + "-buried", column + 1, floor + 1,
                    room.Floor > 100 ? "gold" : "iron", 6));
            }
            var shapes = TerrainShapeGeometry.Build(cells, protection, W, H);
            return new TerrainBlueprint(settings, cells, protection, surface, rooms, soft,
                deposits.ToArray(), shapes: shapes);
        }

        private static int Index(MineMapDefinition map, int x, int y) =>
            (map.OffsetY + y) * W + map.OffsetX + x;

        private static void RequireBaseline(MineMapDefinition map, byte[] baseline, byte[] cells, bool[] soft)
        {
            for (int y = 0; y < map.BaseHeight; y++) for (int x = 0; x < map.BaseWidth; x++)
            {
                int i = Index(map, x, y); byte original = baseline[y * map.BaseWidth + x];
                if (cells[i] == (original == 0 ? 0 : 2) && soft[i] == (original == 2)) continue;
                if (!map.AllowsSeam(x, y))
                    throw new InvalidOperationException("扩展雕刻超出基线接缝：" + map.Id + " " + x + "," + y);
            }
        }

        private static int FloorAt(MineMapDefinition map, byte[] cells, int center, int wanted, out int column)
        {
            foreach (int offset in new[] { 1, -1, 2, -2, 0 })
                for (int y = wanted; y < Math.Min(map.Height - 4, wanted + 8); y++)
                    if (cells[Index(map, center + offset, y)] != 0 && cells[Index(map, center + offset, y - 1)] == 0)
                    { column = center + offset; return y; }
            throw new InvalidOperationException("新增矿袋缺少实际地面：" + center + "," + wanted);
        }

        private static TerrainDepositBlueprint Deposit(MineMapDefinition map, string id,
            int x, int y, string kind, int capacity) => new TerrainDepositBlueprint(map.Id + "-" + id,
                "gallery", x + map.OffsetX, y + map.OffsetY, kind == "gold" ? "rare" : "common", capacity);
    }
}
