using NUnit.Framework;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>实际 GPU 输出回归；验证强度、距离、探针无关光形、墙后与未知区拒绝、同色融合和旧方向保持。</summary>
    public sealed class WallDiffuseRenderTests
    {
        [Test]
        public void LampAndWallOrderDoNotChangeComposition()
        {
            using (var gpu = new WallDiffuseGpuFixture((x, y) => (byte)(x >= 10 || y >= 16 ? 192 : 128)))
            {
                gpu.Count = 4; gpu.Forward = new Vector2(1, .6f).normalized;
                gpu.Dispatch(true); var first = gpu.Pixels();
                gpu.ReverseOrder = true; gpu.ReverseCandidates = true; gpu.Dispatch(true); var reversed = gpu.Pixels();
                for (int i = 0; i < first.Length; i++) Assert.That(reversed[i].r, Is.EqualTo(first[i].r).Within(.00003));
            }
        }

        [Test]
        public void RemovedWallCannotEmitThroughStaleCandidateGeometry()
        {
            using (var gpu = new WallDiffuseGpuFixture((x, y) => (byte)(x == 10 ? 192 : 128)))
            {
                gpu.Dispatch(true); Assert.That(gpu.Profiles()[1].z, Is.GreaterThan(0));
                for (int y = 0; y < 24; y++) gpu.SetCurrentCell(10, y, 128);
                gpu.Dispatch(true); Assert.That(gpu.Profiles()[1].z, Is.Zero);
                gpu.Dispatch(false); var direct = gpu.Pixels(); gpu.Dispatch(true); var noGhost = gpu.Pixels();
                for (int i = 0; i < direct.Length; i++) Assert.That(noGhost[i].r, Is.EqualTo(direct[i].r).Within(.00001));
            }
        }

        [Test]
        public void ContinuousAddsSameColorOnAirSideAndNoDirectionalMoment()
        {
            using (var gpu = new WallDiffuseGpuFixture((x, y) => (byte)(x >= 10 ? 192 : 128)))
            {
                gpu.Dispatch(false); var direct = gpu.Pixels(); var moment = gpu.Pixels(true);
                gpu.Dispatch(true); var continuous = gpu.Pixels(); var bouncedMoment = gpu.Pixels(true);
                float increment = 0;
                for (int i = 0; i < direct.Length; i++)
                {
                    float extra = continuous[i].r - direct[i].r; increment += Mathf.Max(0, extra);
                    Assert.That(continuous[i].g, Is.EqualTo(continuous[i].r * .75f).Within(.00003));
                    Assert.That(continuous[i].b, Is.EqualTo(continuous[i].r * .45f).Within(.00003));
                    Assert.That(bouncedMoment[i].r, Is.EqualTo(moment[i].r).Within(.00001));
                    Assert.That(bouncedMoment[i].g, Is.EqualTo(moment[i].g).Within(.00001));
                }
                Assert.That(increment, Is.GreaterThan(1));
                Assert.That(gpu.At(continuous, 12, 12).r, Is.Zero);
            }
        }

        [Test]
        public void VisibilityProbeCountDoesNotChangeUnoccludedShape()
        {
            using (var gpu = new WallDiffuseGpuFixture((x, y) => (byte)(x >= 10 ? 192 : 128)))
            {
                gpu.Probes = 2; gpu.Dispatch(true); var first = gpu.Pixels();
                foreach (int probes in new[] { 4, 8 })
                {
                    gpu.Probes = probes; gpu.Dispatch(true); var next = gpu.Pixels();
                    for (int i = 0; i < first.Length; i++) Assert.That(next[i].r, Is.EqualTo(first[i].r).Within(.00001));
                }
            }
        }

        [Test]
        public void ZeroIntensityGainAndFarWallProduceNoIncrement()
        {
            using (var gpu = new WallDiffuseGpuFixture((x, y) => (byte)(x >= 10 ? 192 : 128)))
            {
                foreach (int condition in new[] { 0, 1, 2 })
                {
                    gpu.Intensity = condition == 0 ? 0 : 1.35f; gpu.Gain = condition == 1 ? 0 : 1; gpu.OriginX = condition == 2 ? 3 : 7;
                    gpu.Dispatch(false); var direct = gpu.Pixels(); gpu.Dispatch(true); var reflected = gpu.Pixels();
                    for (int i = 0; i < direct.Length; i++) Assert.That(reflected[i].r, Is.EqualTo(direct[i].r).Within(.00001));
                }
            }
        }

        [Test]
        public void UnknownCellsAndThinSlopeRejectNewContributions()
        {
            using (var gpu = new WallDiffuseGpuFixture((x, y) => (byte)(x >= 15 ? 0 : x == 10 ? 199 : 128)))
            {
                gpu.Dispatch(true); var pixels = gpu.Pixels();
                Assert.That(gpu.At(pixels, 16, 12).r, Is.Zero);
                Assert.That(gpu.At(pixels, 12, 12).r, Is.Zero);
            }
        }

        [Test]
        public void MultipleLightsAddWithoutDuplicatingPhysicalWallSelections()
        {
            using (var gpu = new WallDiffuseGpuFixture((x, y) => (byte)(x >= 10 ? 192 : 128)))
            {
                gpu.Count = 4; gpu.Dispatch(true); var profiles = gpu.Profiles();
                for (int light = 0; light < 4; light++)
                {
                    Assert.That(profiles[light * 24 + 1].z, Is.GreaterThan(0));
                    for (int slot = 1; slot < 8; slot++)
                        Assert.That(profiles[light * 24 + slot * 3 + 1].z, Is.Zero, "同一物理墙不能选两次。");
                }
            }
        }
    }
}
