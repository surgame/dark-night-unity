using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>坡形占据和边界的纯函数；x、y 均为格内向右／向上的 0–1 坐标，渲染和权威运动共享编号及公式。</summary>
    public static class TerrainShapeGeometry
    {
        public static TerrainCellShape Decode(ushort flags) => (TerrainCellShape)((flags >> 1) & 15);
        public static ushort Encode(TerrainCellShape shape, bool protect) => (ushort)(((int)shape << 1) | (protect ? 1 : 0));
        public static bool Ceiling(TerrainCellShape shape) => (int)shape >= 7;
        public static float Edge(TerrainCellShape shape, float x)
        {
            int value = (int)shape;
            if (value >= 7) value -= 6;
            switch (value)
            {
                case 1: return x; case 2: return 1 - x;
                case 3: return x * .5f; case 4: return .5f + x * .5f;
                case 5: return 1 - x * .5f; case 6: return .5f - x * .5f;
                default: return 1;
            }
        }
        public static bool Contains(TerrainCellShape shape, float x, float y)
        {
            if (x < 0 || x > 1 || y < 0 || y > 1) return false;
            if (shape == TerrainCellShape.Full) return true;
            return Ceiling(shape) ? y >= Edge(shape, x) : y <= Edge(shape, x);
        }
        public static byte[] Build(byte[] cells, bool[] protection, int width, int height)
        {
            var shapes = new byte[cells.Length];
            for (int y = 2; y < height - 2; y++) for (int x = 2; x < width - 2; x++)
            {
                int i = y * width + x;
                if (cells[i] == 0 || protection[i] || shapes[i] != 0) continue;
                bool above = cells[i - width] == 0, below = cells[i + width] == 0;
                bool left = cells[i - 1] == 0, right = cells[i + 1] == 0;
                if (above && !below && left != right)
                {
                    if (left && cells[i + 1] != 0 && cells[i + 1 - width] == 0 && !protection[i + 1])
                    { shapes[i] = 3; shapes[i + 1] = 4; }
                    else if (right && cells[i - 1] != 0 && cells[i - 1 - width] == 0 && !protection[i - 1] && shapes[i - 1] == 0)
                    { shapes[i - 1] = 5; shapes[i] = 6; }
                    else shapes[i] = (byte)(left ? 1 : 2);
                }
                else if (below && !above && left != right)
                {
                    if (left && cells[i + 1] != 0 && cells[i + 1 + width] == 0 && !protection[i + 1] && shapes[i + 1] == 0)
                    { shapes[i] = 11; shapes[i + 1] = 12; }
                    else if (right && cells[i - 1] != 0 && cells[i - 1 + width] == 0 && !protection[i - 1] && shapes[i - 1] == 0)
                    { shapes[i - 1] = 9; shapes[i] = 10; }
                    else shapes[i] = (byte)(left ? 8 : 7);
                }
            }
            return shapes;
        }
    }
}
