using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>公开的纯地图生成入口；一次调用产出完整冻结蓝图，不创建 Unity 对象、网络状态或存档。</summary>
    public static class TerrainGenerator
    {
        public static TerrainBlueprint Generate(TerrainGenerationSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var s = settings.CopyValidated(); var b = new TerrainGenerationBuffer();
            TerrainSurfaceGenerator.Fill(b, s);
            TerrainCaveGenerator.Carve(b, s);
            FillOres(b, s);
            for (int p = 0; p < 3; p++)
                for (int x = b.PadX[p] - b.PadRadius[p]; x <= b.PadX[p] + b.PadRadius[p]; x++)
                    for (int y = b.PadY[p]; y < b.PadY[p] + 5; y++)
                    {
                        b.Set(x, y, (byte)(y == b.PadY[p] ? 7 : 1));
                        b.Protected[y * TerrainGenerationBuffer.W + x] = true;
                    }
            for (int i = 0; i < b.Cells.Length; i++) if (b.Cells[i] == 8) b.Protected[i] = true;
            return new TerrainBlueprint(s, b.Cells, b.Protected, b.Surface, b.Rooms.ToArray());
        }

        private static void FillOres(TerrainGenerationBuffer b, TerrainGenerationSettings settings)
        {
            const int w = TerrainGenerationBuffer.W, h = TerrainGenerationBuffer.H;
            var r = new TerrainRandom(settings.Seed + ":ores");
            for (int i = 0; i < (int)(300 * settings.OreDensity); i++)
            {
                int x = 8 + (int)(r.Next() * (w - 16)), y = 78 + (int)(r.Next() * (h - 85));
                byte material = (byte)(y > 144 ? r.Next() < .4 ? 6 : 5 : r.Next() < .6 ? 4 : 5);
                int count = 3 + (int)(r.Next() * 9);
                for (int k = 0; k < count; k++)
                {
                    int t = b.At(x, y);
                    if (t > 0 && t != 8 && x >= 0 && x < w && y > b.Surface[x] + 5) b.Set(x, y, material);
                    x += (int)(r.Next() * 3) - 1; y += (int)(r.Next() * 3) - 1;
                }
            }
            var entry = b.Rooms[0];
            for (int y = entry.Y + 2; y < entry.Y + 7; y++)
                for (int x = entry.X + 9; x < entry.X + 12; x++) if (b.At(x, y) > 0) b.Set(x, y, 4);
        }
    }
}
