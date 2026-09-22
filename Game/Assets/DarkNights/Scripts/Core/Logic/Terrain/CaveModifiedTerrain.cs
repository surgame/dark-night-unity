using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>前景生成阶段的组合入口；先从当前只读格形生成基础外轮廓，再执行 modifier，材质和动态光在结果之后计算。</summary>
    public static class CaveModifiedTerrain
    {
        public static CaveMaskField Bake(Func<int, int, bool> solid, int width, int height, string seed,
            CaveOutlineSettings outline, CaveModifierStack modifiers, int undergroundTop, Action checkpoint = null)
        {
            byte[] mask;
            if (outline != null) mask = CaveOutlineBaker.Bake(solid, width, height, 0, 0, width, height, outline, checkpoint);
            else
            {
                mask = new byte[width * height];
                for (int y = 0; y < height; y++)
                {
                    checkpoint?.Invoke();
                    for (int x = 0; x < width; x++) mask[y * width + x] = solid(x, y) ? (byte)1 : (byte)0;
                }
            }
            return (modifiers ?? CaveModifierStack.Empty).Apply(new CaveMaskField(mask, width, height, undergroundTop), seed, checkpoint);
        }
    }
}
