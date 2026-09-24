using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>正式 modifier 资产、冻结身份和生成器替换合同；隔离对象在 finally 释放，不写当前场景或用户资源。</summary>
    public sealed class TerrainModifierTests
    {
        [Test]
        public void AssetsPersistIndependentForegroundAndBackgroundSlots()
        {
            string root = "Assets/DarkNights/Res/Terrain/StrataCave/";
            var style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(root + "Style.asset");
            Assert.That(style.Modifiers.Single(), Is.TypeOf<RoundedClusterModifierAsset>());
            Assert.That(style.Background.Generator, Is.TypeOf<ContourBackgroundGeneratorAsset>());
            Assert.That(style.Background.NearModifiers.Single(), Is.Not.SameAs(style.Modifiers[0]));
            Assert.That(AssetDatabase.LoadAssetAtPath<DownwardEdgeModifierAsset>(root + "Modifiers/DownwardRock.asset").Length, Is.EqualTo(7));
            Assert.That(AssetDatabase.LoadAssetAtPath<DownwardEdgeModifierAsset>(root + "Modifiers/DownwardIce.asset").Sharpness, Is.EqualTo(90));
            Assert.That(style.Background.CaptureModifiers().Length, Is.EqualTo(3));
        }
        [Test]
        public void CapturedStackDoesNotFollowLaterAssetEdits()
        {
            var asset = ScriptableObject.CreateInstance<DownwardEdgeModifierAsset>();
            try
            {
                var stack = CaveModifierAsset.CaptureStack(new CaveModifierAsset[] { asset });
                string identity = stack.Identity; var before = stack.Apply(Field(), "seed").CopyPixels();
                asset.Length = 0;
                Assert.That(stack.Identity, Is.EqualTo(identity));
                CollectionAssert.AreEqual(before, stack.Apply(Field(), "seed").CopyPixels());
                Assert.That(CaveModifierAsset.CaptureStack(new CaveModifierAsset[] { asset }).Identity, Is.Not.EqualTo(identity));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
        [Test]
        public void GeneratorCaptureFreezesAllNineParameters()
        {
            var asset = ScriptableObject.CreateInstance<ContourBackgroundGeneratorAsset>();
            try
            {
                var before = asset.Capture(); string identity = before.Identity;
                asset.NearAmount = 0; asset.Seed = "changed";
                Assert.That(before.Identity, Is.EqualTo(identity));
                Assert.That(asset.Capture().Identity, Is.Not.EqualTo(identity));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
        [Test]
        public void VisualIdentityIncludesGeneratorAndEveryModifierSlot()
        {
            var style = ScriptableObject.CreateInstance<CaveTerrainStyle>(); var bg = ScriptableObject.CreateInstance<CaveBackgroundStyle>();
            var modifier = ScriptableObject.CreateInstance<DownwardEdgeModifierAsset>();
            try
            {
                style.Background = bg; string original = style.VisualIdentity;
                style.Modifiers = new CaveModifierAsset[] { modifier }; Assert.That(style.VisualIdentity, Is.Not.EqualTo(original));
                string front = style.VisualIdentity;
                bg.MiddleModifiers = new CaveModifierAsset[] { modifier }; Assert.That(style.VisualIdentity, Is.Not.EqualTo(front));
                string middle = style.VisualIdentity; modifier.Density--;
                Assert.That(style.VisualIdentity, Is.Not.EqualTo(middle));
                string shape = style.VisualIdentity; bg.ContourStatic = false;
                Assert.That(style.VisualIdentity, Is.Not.EqualTo(shape));
            }
            finally { UnityEngine.Object.DestroyImmediate(style); UnityEngine.Object.DestroyImmediate(bg); UnityEngine.Object.DestroyImmediate(modifier); }
        }
        [Test]
        public void UnknownBackgroundImplementationUsesSameModifierAndPagePath()
        {
            ICaveBackgroundGenerator generator = new ModifierBackgroundStub(); var layout = generator.Build(null, null);
            var stack = new CaveModifierStack(new[] { new RoundedClusterModifier(grain: false) });
            var modified = new ModifiedBackgroundLayout(layout, new[] { stack, CaveModifierStack.Empty, stack }, "seed");
            var before = BackgroundPageBaker.Bake(layout, 0, 0, 128, 128); var after = BackgroundPageBaker.Bake(modified, 0, 0, 128, 128);
            CollectionAssert.AreEqual(before[1], after[1]);
            Assert.That(before[0].SequenceEqual(after[0]), Is.False); Assert.That(before[2].SequenceEqual(after[2]), Is.False);
        }
        [Test]
        public void EmptyAndZeroStrengthRemainExactOriginal()
        {
            var source = Field(); Assert.That(CaveModifierStack.Empty.Apply(source, "seed"), Is.SameAs(source));
            Assert.That(new DownwardEdgeModifier(length: 0).Apply(source, "seed"), Is.SameAs(source));
            Assert.That(new RoundedClusterModifier(depth: 0).Apply(source, "seed"), Is.SameAs(source));
        }
        [Test]
        public void GrainSwitchChangesOnlyMaterialNotMask()
        {
            var source = Field(); var grain = new RoundedClusterModifier().Apply(source, "seed");
            var plain = new RoundedClusterModifier(grain: false).Apply(source, "seed");
            CollectionAssert.AreEqual(grain.CopyPixels(), plain.CopyPixels()); Assert.That(grain.Grains.Count, Is.EqualTo(1));
            Assert.That(plain.Grains.Count, Is.EqualTo(0));
            var a = CaveRockBaker.Bake(source.Solid, 128, 128, "seed", 0, 0, 128, 128, modified: grain);
            var b = CaveRockBaker.Bake(source.Solid, 128, 128, "seed", 0, 0, 128, 128, modified: plain);
            Assert.That(a.SequenceEqual(b), Is.False);
            for (int p = 0; p < 128 * 128; p++) Assert.That(a[p * 4 + 3], Is.EqualTo(b[p * 4 + 3]));
        }

        [Test]
        public void LegacyVersionStaysDefaultAndLocalV2HasDistinctIdentity()
        {
            var legacy = new RoundedClusterModifier();
            var local = new RoundedClusterModifier(algorithmVersion: RoundedClusterAlgorithmVersion.LocalV2);
            Assert.That(legacy.AlgorithmVersion, Is.EqualTo(RoundedClusterAlgorithmVersion.LegacyV1));
            Assert.That(legacy.Identity, Does.StartWith("rounded-v1,"));
            Assert.That(local.Identity, Does.StartWith("rounded-local-v2,"));
            Assert.That(local.Identity, Is.Not.EqualTo(legacy.Identity));
            Assert.That(new CaveModifierStack(new[] { legacy }).SupportsLocalRoundedCluster, Is.False);
            Assert.That(new CaveModifierStack(new[] { local }).SupportsLocalRoundedCluster, Is.True);
        }

        [Test]
        public void LocalV2PagesMatchFullBakeBeforeAndAfterCrossPageEdits()
        {
            const int width = 512, height = 384, candidateTop = 8, pageSize = 128;
            var modifier = new RoundedClusterModifier(algorithmVersion: RoundedClusterAlgorithmVersion.LocalV2);
            var original = Ceiling(width, height);
            AssertLocalMatchesFull(modifier, original, width, height, candidateTop, pageSize);

            var edited = (byte[])original.Clone();
            for (int y = 116; y < 137; y++) for (int x = 249; x < 263; x++) edited[y * width + x] = 0;
            for (int y = 121; y < 134; y++) for (int x = 302; x < 307; x++) edited[y * width + x] = 1;
            AssertLocalMatchesFull(modifier, edited, width, height, candidateTop, pageSize);
        }

        [TestCase(0, 90)]
        [TestCase(8, 0)]
        public void LocalV2ZeroStrengthHasNoGrainLayer(int depth, int density)
        {
            var modifier = new RoundedClusterModifier(depth: depth, density: density,
                algorithmVersion: RoundedClusterAlgorithmVersion.LocalV2);
            var source = Field(); var full = modifier.Apply(source, "zero-strength");
            var region = new CaveMaskRegion(source.CopyPixels(), source.Width, source.Height, 0, 0, source.Width, source.Height);
            var page = modifier.ApplyRegion(region, 0, 0, 64, 64, "zero-strength", source.Top);

            Assert.That(full.Grains.Count, Is.Zero);
            Assert.That(page.HasGrain, Is.False);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                Assert.That(page.Solid(x, y), Is.EqualTo(full.Solid(x, y)));
        }

        [Test]
        public void StackOrderIsExplicitAndRebuildStartsAtOriginal()
        {
            var a = new DownwardEdgeModifier(); var b = new RoundedClusterModifier(); var source = Field();
            var stack = new CaveModifierStack(new ICaveMaskModifier[] { a, b });
            CollectionAssert.AreEqual(b.Apply(a.Apply(source, "seed"), "seed").CopyPixels(), stack.Apply(source, "seed").CopyPixels());
            CollectionAssert.AreEqual(stack.Apply(source, "seed").CopyPixels(), stack.Apply(source, "seed").CopyPixels());
            Assert.That(stack.Identity, Is.Not.EqualTo(new CaveModifierStack(new ICaveMaskModifier[] { b, a }).Identity));
        }
        [Test]
        public void InvalidReferencesAndParametersFailBeforeTasksStart()
        {
            Assert.Throws<InvalidOperationException>(() => CaveModifierAsset.CaptureStack(new CaveModifierAsset[] { null }));
            Assert.Throws<ArgumentException>(() => new DownwardEdgeModifier(length: 25));
            Assert.Throws<ArgumentException>(() => new RoundedClusterModifier(depth: -1));
            Assert.Throws<ArgumentException>(() => new BackgroundContourSettings(nearWidth: 0));
            Assert.Throws<ArgumentException>(() => new CaveModifierStack(Enumerable.Repeat<ICaveMaskModifier>(new DownwardEdgeModifier(), 9)));
        }

        private static void AssertLocalMatchesFull(RoundedClusterModifier modifier, byte[] input,
            int worldWidth, int worldHeight, int candidateTop, int pageSize)
        {
            var full = modifier.Apply(new CaveMaskField(input, worldWidth, worldHeight, candidateTop), "local-v2-test");
            var fullRock = CaveRockBaker.Bake((x, y) => input[y * worldWidth + x] != 0,
                worldWidth, worldHeight, "local-v2-test", 0, 0, worldWidth, worldHeight,
                stoneSize: 5, modified: full);
            var stack = new CaveModifierStack(new ICaveMaskModifier[] { modifier });
            for (int top = 0; top < worldHeight; top += pageSize)
            for (int left = 0; left < worldWidth; left += pageSize)
            {
                int width = Math.Min(pageSize, worldWidth - left), height = Math.Min(pageSize, worldHeight - top);
                int halo = modifier.DependencyRadiusPixels;
                int sourceLeft = Math.Max(0, left - halo), sourceTop = Math.Max(0, top - halo);
                int sourceRight = Math.Min(worldWidth, left + width + halo), sourceBottom = Math.Min(worldHeight, top + height + halo);
                int sourceWidth = sourceRight - sourceLeft, sourceHeight = sourceBottom - sourceTop;
                var pixels = new byte[sourceWidth * sourceHeight];
                for (int y = 0; y < sourceHeight; y++)
                    Array.Copy(input, (sourceTop + y) * worldWidth + sourceLeft, pixels, y * sourceWidth, sourceWidth);
                var region = new CaveMaskRegion(pixels, worldWidth, worldHeight, sourceLeft, sourceTop, sourceWidth, sourceHeight);
                var page = modifier.ApplyRegion(region, left, top, width, height, "local-v2-test", candidateTop);
                var rockRegion = CaveModifiedTerrain.BakeRockRegion((x, y) => input[y * worldWidth + x] != 0,
                    worldWidth, worldHeight, "local-v2-test", null, stack, left, top, width, height,
                    CaveRockBaker.DistanceCap, candidateTop);
                var pageRock = CaveRockBaker.BakeRegion(rockRegion, left, top, width, height, "local-v2-test", 5);
                for (int y = top; y < top + height; y++) for (int x = left; x < left + width; x++)
                {
                    int index = y * worldWidth + x;
                    Assert.That(page.Solid(x, y), Is.EqualTo(full.Solid(x, y)), "mask mismatch at " + x + "," + y);
                    Assert.That(page.GrainWeight(x, y), Is.EqualTo(full.Grains[0].Weight(index)), "grain mismatch at " + x + "," + y);
                    Assert.That(page.GrainTone(x, y), Is.EqualTo(full.Grains[0].Tone(index)), "tone mismatch at " + x + "," + y);
                    int pageIndex = ((y - top) * width + x - left) * 4, fullIndex = index * 4;
                    for (int channel = 0; channel < 4; channel++)
                        Assert.That(pageRock[pageIndex + channel], Is.EqualTo(fullRock[fullIndex + channel]),
                            "rock pixel mismatch at " + x + "," + y + ", channel " + channel);
                }
            }
        }

        private static byte[] Ceiling(int width, int height)
        {
            var result = new byte[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (y < 128 || y >= 220) result[y * width + x] = 1;
            return result;
        }

        private static CaveMaskField Field()
        {
            var mask = new byte[128 * 128];
            for (int y = 0; y < 35; y++) for (int x = 0; x < 128; x++) mask[y * 128 + x] = 1;
            return new CaveMaskField(mask, 128, 128);
        }
    }
}
