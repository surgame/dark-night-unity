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

        /// <summary>准备岩壁输出页需要的距离场区域；只重算目标页、材质距离 halo、轮廓 halo 与 LocalV2 依赖范围。</summary>
        public static CaveMaskRegion BakeRockRegion(Func<int, int, bool> solid, int worldWidth, int worldHeight, string seed,
            CaveOutlineSettings outline, CaveModifierStack modifiers, int left, int top, int width, int height, int distanceHalo, int candidateTop,
            Action checkpoint = null)
        {
            if (solid == null || worldWidth < 1 || worldHeight < 1 || left < 0 || top < 0 || width < 1 || height < 1 ||
                left + width > worldWidth || top + height > worldHeight || distanceHalo < 0)
                throw new ArgumentException("局部岩壁页或距离 halo 无效。");
            modifiers = modifiers ?? CaveModifierStack.Empty;
            var local = modifiers.RequireLocalRoundedCluster();
            int outputLeft = Math.Max(0, left - distanceHalo), outputTop = Math.Max(0, top - distanceHalo);
            int outputRight = Math.Min(worldWidth, left + width + distanceHalo), outputBottom = Math.Min(worldHeight, top + height + distanceHalo);
            int halo = local == null ? 0 : local.DependencyRadiusPixels;
            int inputLeft = Math.Max(0, outputLeft - halo), inputTop = Math.Max(0, outputTop - halo);
            int inputRight = Math.Min(worldWidth, outputRight + halo), inputBottom = Math.Min(worldHeight, outputBottom + halo);
            int inputWidth = inputRight - inputLeft, inputHeight = inputBottom - inputTop;
            byte[] basePixels = outline == null || !outline.Enabled
                ? ReadSolidRegion(solid, inputLeft, inputTop, inputWidth, inputHeight, checkpoint)
                : CaveOutlineBaker.Bake(solid, worldWidth, worldHeight, inputLeft, inputTop, inputWidth, inputHeight, outline, checkpoint);
            var baseRegion = new CaveMaskRegion(basePixels, worldWidth, worldHeight, inputLeft, inputTop, inputWidth, inputHeight);
            return local == null ? baseRegion.Slice(outputLeft, outputTop, outputRight - outputLeft, outputBottom - outputTop) :
                local.ApplyRegion(baseRegion, outputLeft, outputTop, outputRight - outputLeft, outputBottom - outputTop, seed, candidateTop, checkpoint);
        }

        private static byte[] ReadSolidRegion(Func<int, int, bool> solid, int left, int top, int width, int height, Action checkpoint)
        {
            var pixels = new byte[checked(width * height)];
            for (int y = 0; y < height; y++)
            {
                checkpoint?.Invoke();
                for (int x = 0; x < width; x++) pixels[y * width + x] = solid(left + x, top + y) ? (byte)1 : (byte)0;
            }
            return pixels;
        }
    }
}
