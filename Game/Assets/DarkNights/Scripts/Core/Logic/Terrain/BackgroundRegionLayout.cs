using System;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>局部背景栅格回到原地图坐标的只读适配；局部布局独占窗口，未准备区域透明，分页与着色仍使用全局位置和种子。</summary>
    public sealed class BackgroundRegionLayout : ICaveBackgroundLayout
    {
        private readonly CaveMaskRegion[] layers = new CaveMaskRegion[3];
        private readonly int left, top, width, height;
        public int Width { get; }
        public int Height { get; }
        public int Top { get; }
        public uint LayoutSeed { get; }

        public BackgroundRegionLayout(ICaveBackgroundLayout local, int sourceLeft, int sourceTop,
            int left, int top, int width, int height, int worldWidth, int worldHeight, int decorationTop,
            CaveModifierStack[] stacks, string seed, Action checkpoint = null)
        {
            if (local == null) throw new ArgumentNullException(nameof(local));
            if (left < 0 || top < 0 || left + width > worldWidth || top + height > worldHeight || stacks == null || stacks.Length != 3)
                throw new ArgumentException("背景局部窗口越界。");
            this.left = left; this.top = top; this.width = width; this.height = height;
            Width = worldWidth; Height = worldHeight; Top = decorationTop; LayoutSeed = local.LayoutSeed;
            for (int layer = 0; layer < 3; layer++)
            {
                var pixels = new byte[local.Width * local.Height];
                for (int y = 0; y < local.Height; y++)
                {
                    checkpoint?.Invoke();
                    for (int x = 0; x < local.Width; x++)
                        pixels[y * local.Width + x] = sourceTop + y >= Top && local.Solid(layer, x, y) ? (byte)1 : (byte)0;
                }
                var input = new CaveMaskRegion(pixels, worldWidth, worldHeight, sourceLeft, sourceTop, local.Width, local.Height);
                var modifier = stacks[layer].AsLocalForeground().RequireLocalRoundedCluster();
                layers[layer] = modifier == null ? input.Slice(left, top, width, height) :
                    modifier.ApplyRegion(input, left, top, width, height, seed + "|background-" + layer, Top, checkpoint);
            }
        }
        /// <summary>局部背景显式使用有界圆簇副本，保留作者参数；不改作者栈，不声明与全图 LegacyV1 像素等价。</summary>
        public static int RequiredHalo(CaveModifierStack[] stacks)
        {
            int halo = 0;
            foreach (var stack in stacks)
            {
                var modifier = stack.AsLocalForeground().RequireLocalRoundedCluster();
                if (modifier != null) halo = Math.Max(halo, modifier.DependencyRadiusPixels);
            }
            return halo;
        }
        public bool Solid(int layer, int x, int y)
            => y >= Top && x >= left && y >= top && x < left + width && y < top + height && layers[layer].Solid(x, y);
    }
}
