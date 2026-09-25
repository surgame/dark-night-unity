using System;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>一页材质的世界坐标分面索引；可按不同原生岩粒尺寸并存，局部细分不改变远离圆簇的原始材质。</summary>
    internal sealed class CaveRockToneField
    {
        private readonly CaveRockFacet[] facets;
        private readonly int x0, y0, width;
        private readonly double size;
        public CaveRockToneField(int left, int top, int w, int h, uint seed, double size)
        {
            this.size = size; x0 = (int)Math.Floor(left / size) - 1; y0 = (int)Math.Floor(top / size) - 1;
            width = (int)Math.Floor((left + w - 1) / size) - x0 + 2;
            int height = (int)Math.Floor((top + h - 1) / size) - y0 + 2;
            facets = new CaveRockFacet[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                facets[y * width + x] = CaveRockBaker.Create(x + x0, y + y0, seed, size);
        }
        public int Tone(int x, int y)
        {
            double first = double.MaxValue, second = double.MaxValue; CaveRockFacet nearest = default;
            int column = (int)Math.Floor(x / size) - x0, row = (int)Math.Floor(y / size) - y0;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                var f = facets[(row + dy) * width + column + dx];
                double d = (x - f.X) * (x - f.X) + (y - f.Y) * (y - f.Y) * 1.18;
                if (d < first) { second = first; first = d; nearest = f; }
                else if (d < second) second = d;
            }
            int tone = Math.Max(0, Math.Min(7, nearest.Tone + (y < nearest.Y - 1 ? 1 : 0) - (y > nearest.Y + 1 ? 1 : 0)));
            return Math.Sqrt(second) - Math.Sqrt(first) < .36 ? 0 : tone;
        }
    }
}
