using System;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>在任意背景生成器输出上分别运行三个 modifier 栈；整层计算一次，材质柔边随后处理，空栈直接委托原布局。</summary>
    public sealed class ModifiedBackgroundLayout : ICaveBackgroundLayout
    {
        private readonly ICaveBackgroundLayout original;
        private readonly CaveMaskField[] fields = new CaveMaskField[3];
        public int Width => original.Width;
        public int Height => original.Height;
        public int Top => original.Top;
        public uint LayoutSeed => original.LayoutSeed;
        public ModifiedBackgroundLayout(ICaveBackgroundLayout original, CaveModifierStack[] stacks, string seed, Action checkpoint = null)
        {
            this.original = original ?? throw new ArgumentNullException(nameof(original));
            if (stacks == null || stacks.Length != 3) throw new ArgumentException("背景须提供近、中、深三个修饰栈。");
            for (int layer = 0; layer < 3; layer++)
            {
                if (stacks[layer] == null) throw new ArgumentException("背景修饰栈为空引用。");
                if (!stacks[layer].Enabled) continue;
                var mask = new byte[Width * Height];
                for (int y = 0; y < Height; y++)
                {
                    checkpoint?.Invoke();
                    for (int x = 0; x < Width; x++) mask[y * Width + x] = original.Solid(layer, x, y) ? (byte)1 : (byte)0;
                }
                var result = stacks[layer].Apply(new CaveMaskField(mask, Width, Height, Top), seed + "|background-" + layer, checkpoint);
                // 背景复用自己的平面色阶，不保留前景岩粒字段。
                fields[layer] = new CaveMaskField(result.CopyPixels(), Width, Height, Top);
            }
        }
        public bool Solid(int layer, int x, int y) => fields[layer]?.Solid(x, y) ?? original.Solid(layer, x, y);
    }
}
