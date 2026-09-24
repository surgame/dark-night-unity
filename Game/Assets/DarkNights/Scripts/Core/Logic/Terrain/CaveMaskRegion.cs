using System;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>以世界像素坐标索引的局部只读岩壁遮罩；缓冲区只覆盖显式区域，越过区域的内部读取会失败。</summary>
    public sealed class CaveMaskRegion
    {
        private readonly byte[] pixels;
        private readonly float[] grainWeights;
        private readonly byte[] grainTones;
        public int WorldWidth { get; }
        public int WorldHeight { get; }
        public int Left { get; }
        public int Top { get; }
        public int Width { get; }
        public int Height { get; }
        public double GrainStoneSize { get; }
        public bool HasGrain => grainWeights != null;

        public CaveMaskRegion(byte[] pixels, int worldWidth, int worldHeight, int left, int top, int width, int height,
            float[] grainWeights = null, byte[] grainTones = null, double grainStoneSize = 4)
        {
            if (worldWidth < 1 || worldHeight < 1 || pixels == null || width < 1 || height < 1 ||
                left < 0 || top < 0 || left + width > worldWidth || top + height > worldHeight ||
                pixels.Length != checked(width * height) || (grainWeights == null) != (grainTones == null) ||
                grainWeights != null && (grainWeights.Length != pixels.Length || grainTones.Length != pixels.Length ||
                    double.IsNaN(grainStoneSize) || grainStoneSize < 2 || grainStoneSize > 12))
                throw new ArgumentException("局部轮廓区域的尺寸、边界或岩粒字段无效。");
            WorldWidth = worldWidth; WorldHeight = worldHeight; Left = left; Top = top; Width = width; Height = height;
            this.pixels = (byte[])pixels.Clone();
            this.grainWeights = grainWeights == null ? null : (float[])grainWeights.Clone();
            this.grainTones = grainTones == null ? null : (byte[])grainTones.Clone(); GrainStoneSize = grainStoneSize;
        }

        public bool Solid(int x, int y) => x >= 0 && y >= 0 && x < WorldWidth && y < WorldHeight && pixels[Index(x, y)] != 0;
        public float GrainWeight(int x, int y) => grainWeights == null || x < 0 || y < 0 || x >= WorldWidth || y >= WorldHeight ? 0 : grainWeights[Index(x, y)];
        public byte GrainTone(int x, int y) => grainTones == null || x < 0 || y < 0 || x >= WorldWidth || y >= WorldHeight ? (byte)0 : grainTones[Index(x, y)];
        internal byte[] CopyPixels() => (byte[])pixels.Clone();
        internal float[] CopyGrainWeights() => grainWeights == null ? null : (float[])grainWeights.Clone();
        internal byte[] CopyGrainTones() => grainTones == null ? null : (byte[])grainTones.Clone();
        internal byte PixelAt(int x, int y) => pixels[Index(x, y)];

        internal CaveMaskRegion Slice(int left, int top, int width, int height)
        {
            if (left < Left || top < Top || left + width > Left + Width || top + height > Top + Height)
                throw new ArgumentOutOfRangeException(nameof(left), "切片必须完全落在已准备区域内。");
            var sliced = new byte[checked(width * height)];
            var weights = grainWeights == null ? null : new float[sliced.Length];
            var tones = grainTones == null ? null : new byte[sliced.Length];
            for (int y = 0; y < height; y++)
            {
                int source = (top - Top + y) * Width + left - Left, target = y * width;
                Array.Copy(pixels, source, sliced, target, width);
                if (weights != null) { Array.Copy(grainWeights, source, weights, target, width); Array.Copy(grainTones, source, tones, target, width); }
            }
            return new CaveMaskRegion(sliced, WorldWidth, WorldHeight, left, top, width, height, weights, tones, GrainStoneSize);
        }

        private int Index(int x, int y)
        {
            if (x < 0 || y < 0 || x >= WorldWidth || y >= WorldHeight) return -1;
            int localX = x - Left, localY = y - Top;
            if (localX < 0 || localY < 0 || localX >= Width || localY >= Height)
                throw new InvalidOperationException("局部轮廓读取 " + x + "," + y + " 超出依赖 Halo " +
                    Left + "," + Top + " .. " + (Left + Width) + "," + (Top + Height) + "。");
            return localY * Width + localX;
        }
    }
}
