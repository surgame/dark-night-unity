using System;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>真实前景上传补丁的遮挡合同；覆盖Alpha阈值、首次整页确认、页边及连续挖填，不启动玩法或读取GPU。</summary>
    public sealed class CaveForegroundCoverageTests
    {
        private static readonly Type CoverageType = typeof(CaveVisualSource).Assembly.GetType("DarkNights.View.Terrain.CaveForegroundCoverage", true);
        private static object NewCoverage() => Activator.CreateInstance(CoverageType);
        private static bool Read(object coverage, string method, int key) =>
            (bool)CoverageType.GetMethod(method).Invoke(coverage, new object[] { key });
        private static void Commit(object coverage, RectInt rect, byte alpha)
        {
            var pixels = new byte[rect.width * rect.height * 4];
            for (int i = 3; i < pixels.Length; i += 4) pixels[i] = alpha;
            CoverageType.GetMethod("Commit").Invoke(coverage, new object[] { rect, pixels });
        }

        [Test]
        public void PartialInitialOutputDoesNotAuthorizeWholePageCulling()
        {
            object coverage = NewCoverage();
            Commit(coverage, new RectInt(0, 0, 255, 256), 255);
            Assert.That(Read(coverage, "HasPage", 0), Is.False);
            Assert.That(Read(coverage, "FullyOpaque", 0), Is.False);
        }

        [TestCase(127, false)] [TestCase(128, true)] [TestCase(255, true)]
        public void CoverageMatchesForegroundAlphaClipThreshold(byte alpha, bool expected)
        {
            object coverage = NewCoverage();
            Commit(coverage, new RectInt(0, 0, 256, 256), alpha);
            Assert.That(Read(coverage, "HasPage", 0), Is.True);
            Assert.That(Read(coverage, "FullyOpaque", 0), Is.EqualTo(expected));
        }

        [Test]
        public void DigAndFillAtFourPageCornerOnlyChangeThoseCommittedPixels()
        {
            object coverage = NewCoverage(); int[] pages = { 0, 1, 10, 11 };
            Commit(coverage, new RectInt(0, 0, 512, 512), 255);
            foreach (int key in pages) Assert.That(Read(coverage, "FullyOpaque", key), Is.True);
            Commit(coverage, new RectInt(255, 255, 2, 2), 0);
            foreach (int key in pages) Assert.That(Read(coverage, "FullyOpaque", key), Is.False);
            Commit(coverage, new RectInt(255, 255, 2, 2), 255);
            foreach (int key in pages) Assert.That(Read(coverage, "FullyOpaque", key), Is.True);
            Commit(coverage, new RectInt(255, 255, 1, 1), 0);
            Assert.That(Read(coverage, "FullyOpaque", 0), Is.False);
            foreach (int key in new[] { 1, 10, 11 }) Assert.That(Read(coverage, "FullyOpaque", key), Is.True);
        }
    }
}
