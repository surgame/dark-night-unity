using System;
using System.Collections.Generic;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>一次完整轮廓生成的只读快照；数组不外借，任意页面共享同一结果，避免全局岩簇优先级在页边重新求解。</summary>
    public sealed class CaveMaskField
    {
        private readonly byte[] pixels;
        public int Width { get; }
        public int Height { get; }
        public int Top { get; }
        public IReadOnlyList<CaveGrainLayer> Grains { get; }
        public CaveMaskField(byte[] pixels, int width, int height, int top = 0)
            : this(pixels, width, height, top, Array.Empty<CaveGrainLayer>()) { }
        private CaveMaskField(byte[] pixels, int width, int height, int top, IReadOnlyList<CaveGrainLayer> grains)
        {
            if (width < 1 || width > 2560 || height < 1 || height > 1536 || pixels == null ||
                pixels.Length != checked(width * height) || top < 0 || top >= height) throw new ArgumentException("修饰轮廓尺寸无效。");
            this.pixels = (byte[])pixels.Clone(); Width = width; Height = height; Top = top; Grains = grains;
        }
        public bool Solid(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && pixels[y * Width + x] != 0;
        public byte[] CopyPixels() => (byte[])pixels.Clone();
        public CaveMaskField With(byte[] mask, CaveGrainLayer grain = null)
        {
            var next = new List<CaveGrainLayer>(Grains);
            if (grain != null)
            {
                if (grain.Length != pixels.Length) throw new ArgumentException("岩粒和轮廓尺寸不一致。");
                next.Add(grain);
            }
            return new CaveMaskField(mask, Width, Height, Top, next.AsReadOnly());
        }
        public bool SamePixel(CaveMaskField other, int index)
        {
            if (other == null || other.Width != Width || other.Height != Height || pixels[index] != other.pixels[index]) return false;
            if (pixels[index] == 0) return true;
            if (Grains.Count != other.Grains.Count) return false;
            for (int i = 0; i < Grains.Count; i++)
            {
                var a = Grains[i]; var b = other.Grains[i];
                if (a.Weight(index) != b.Weight(index) || (a.Weight(index) > 0 &&
                    (a.Tone(index) != b.Tone(index) || a.StoneSize != b.StoneSize))) return false;
            }
            return true;
        }
    }
}
