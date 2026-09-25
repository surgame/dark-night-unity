using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using NUnit.Framework;

namespace DarkNights.RepairTests
{
    /// <summary>直接链接游戏 Core 源码验证 LocalV2 任意补丁；不替代 Unity、GPU、相机或联机验收。</summary>
    public sealed class TerrainRepairTests
    {
        private const int StoneSize = 4;
        [Test]
        public void LocalConversionKeepsOriginalAssetContract()
        {
            var original = new RoundedClusterModifier(size: 24, seed: "kept");
            var stack = new CaveModifierStack(new ICaveMaskModifier[] { original });
            var local = stack.AsLocalForeground().RequireLocalRoundedCluster();
            Assert.That(original.AlgorithmVersion, Is.EqualTo(RoundedClusterAlgorithmVersion.LegacyV1));
            Assert.That(local.AlgorithmVersion, Is.EqualTo(RoundedClusterAlgorithmVersion.LocalV2));
            Assert.That(local.Size, Is.EqualTo(original.Size));
            Assert.That(local.ModifierSeed, Is.EqualTo(original.ModifierSeed));
            Assert.That(local.Identity, Is.Not.EqualTo(original.Identity));
        }
        [Test]
        public void UnsupportedStackCannotSilentlyFallBack()
        {
            var stack = new CaveModifierStack(new ICaveMaskModifier[] { new DownwardEdgeModifier() });
            Assert.Throws<NotSupportedException>(() => stack.AsLocalForeground());
        }
        [TestCase(0, 0, 41, 53, true)]
        [TestCase(117, 81, 63, 77, true)]
        [TestCase(245, 89, 97, 91, false)]
        [TestCase(330, 210, 54, 46, true)]
        public void ArbitraryRectMatchesFullRgba(int left, int top, int width, int height, bool grain)
        {
            const int worldWidth = 384, worldHeight = 256;
            var mask = MakeMask(worldWidth, worldHeight);
            var stack = new CaveModifierStack(new ICaveMaskModifier[]
            { new RoundedClusterModifier(grain: grain, algorithmVersion: RoundedClusterAlgorithmVersion.LocalV2) });
            var fullField = CaveModifiedTerrain.Bake(Solid, worldWidth, worldHeight, "repair-roi", null, stack, 8);
            var full = CaveRockBaker.Bake(Solid, worldWidth, worldHeight, "repair-roi", 0, 0, worldWidth, worldHeight,
                stoneSize: StoneSize, modified: fullField);
            var field = CaveModifiedTerrain.BakeRockRegion(Solid, worldWidth, worldHeight, "repair-roi", null,
                stack, left, top, width, height, CaveRockBaker.DistanceCap, 8);
            var patch = CaveRockBaker.BakeRegion(field, left, top, width, height, "repair-roi", StoneSize);
            var expected = new byte[patch.Length];
            for (int y = 0; y < height; y++)
                Array.Copy(full, ((top + y) * worldWidth + left) * 4, expected, y * width * 4, width * 4);
            CollectionAssert.AreEqual(expected, patch);
            bool Solid(int x, int y) => mask[y * worldWidth + x] != 0;
        }
        [Test]
        public void EditedPatchDoesNotChangePixelsOutsideDependencyRadius()
        {
            const int w = 384, h = 256;
            var mask = MakeMask(w, h);
            var rounded = new RoundedClusterModifier(algorithmVersion: RoundedClusterAlgorithmVersion.LocalV2);
            var stack = new CaveModifierStack(new ICaveMaskModifier[] { rounded });
            byte[] Full()
            {
                bool Solid(int x, int y) => mask[y * w + x] != 0;
                var field = CaveModifiedTerrain.Bake(Solid, w, h, "patch-edit", null, stack, 8);
                return CaveRockBaker.Bake(Solid, w, h, "patch-edit", 0, 0, w, h, stoneSize: StoneSize, modified: field);
            }
            var before = Full();
            const int cx = 128, cy = 88;
            for (int y = cy; y < cy + 8; y++) for (int x = cx; x < cx + 8; x++) mask[y * w + x] = 0;
            var after = Full();
            int radius = CaveRockBaker.DistanceCap + rounded.DependencyRadiusPixels + 8;
            int left = Math.Max(0, cx - radius), top = Math.Max(0, cy - radius);
            int right = Math.Min(w, cx + 8 + radius), bottom = Math.Min(h, cy + 8 + radius);
            var local = CaveModifiedTerrain.BakeRockRegion((x, y) => mask[y * w + x] != 0, w, h, "patch-edit", null,
                stack, left, top, right - left, bottom - top, CaveRockBaker.DistanceCap, 8);
            var patch = CaveRockBaker.BakeRegion(local, left, top, right - left, bottom - top, "patch-edit", StoneSize);
            for (int y = top; y < bottom; y++)
                Array.Copy(patch, (y - top) * (right - left) * 4, before, (y * w + left) * 4, (right - left) * 4);
            CollectionAssert.AreEqual(after, before);
        }
        private static byte[] MakeMask(int w, int h)
        {
            var mask = new byte[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                if (y < 92 + x / 23 % 3 || y >= 181 + x / 31 % 4) mask[y * w + x] = 1;
            return mask;
        }
    }
}
