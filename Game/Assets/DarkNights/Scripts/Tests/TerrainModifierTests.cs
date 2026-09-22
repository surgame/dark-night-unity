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
        private static CaveMaskField Field()
        {
            var mask = new byte[128 * 128];
            for (int y = 0; y < 35; y++) for (int x = 0; x < 128; x++) mask[y * 128 + x] = 1;
            return new CaveMaskField(mask, 128, 128);
        }
    }
}
