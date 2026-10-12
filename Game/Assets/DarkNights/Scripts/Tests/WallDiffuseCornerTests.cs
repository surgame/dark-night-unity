using System;
using DarkNights.View.Lighting;
using NUnit.Framework;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>斜墙连接和柔光团的定向 GPU 回归；冻结横墙接坡面的地形，核对连接身份、足迹扩大及薄墙遮挡，不替代人工画面判断。</summary>
    public sealed class WallDiffuseCornerTests
    {
        [Test]
        public void HorizontalWallAndSlopeKeepSeparateGeometryButJoinAtTheirSharedEnd()
        {
            using (var gpu = new WallDiffuseGpuFixture(FloorAndSlope))
            {
                gpu.OriginX = 9; gpu.OriginY = 13; gpu.Forward = new Vector2(1, 3).normalized;
                gpu.Dispatch(true);
                var profiles = gpu.Profiles(); int joined = 0;
                for (int slot = 0; slot < 8; slot++)
                    if (profiles[slot * 3 + 2].w > 0) joined++;
                Assert.That(joined, Is.GreaterThanOrEqualTo(2));
                gpu.CornerRadius = 0; gpu.Dispatch(true); profiles = gpu.Profiles();
                for (int slot = 0; slot < 8; slot++) Assert.That(profiles[slot * 3 + 2].w, Is.Zero);
            }
        }

        [Test]
        public void SoftBlobExpandsTheAirFootprintWithoutAddingSeparateProbeCenters()
        {
            using (var gpu = new WallDiffuseGpuFixture((x, y) => (byte)(x >= 10 ? 192 : 128)))
            {
                gpu.Dispatch(false); var direct = gpu.Pixels();
                gpu.Dispatch(true); var band = gpu.Pixels();
                gpu.Shape = WallDiffuseShape.SoftBlob; gpu.Dispatch(true); var blob = gpu.Pixels();
                int bandPixels = 0, blobPixels = 0;
                for (int i = 0; i < direct.Length; i++)
                {
                    if (i % WallDiffuseGpuFixture.Size >= 40) continue;
                    if (band[i].r - direct[i].r > .001f) bandPixels++;
                    if (blob[i].r - direct[i].r > .001f) blobPixels++;
                }
                Assert.That(blobPixels, Is.GreaterThan(bandPixels));
                gpu.Probes = 4; gpu.Dispatch(true); var moreProbes = gpu.Pixels();
                for (int i = 0; i < blob.Length; i++) Assert.That(moreProbes[i].r, Is.EqualTo(blob[i].r).Within(.00001));
            }
        }

        [TestCase(WallDiffuseShape.WallBand)]
        [TestCase(WallDiffuseShape.SoftBlob)]
        public void WallSurfaceReceivesBoundedReturnAndWallBackStaysDark(WallDiffuseShape shape)
        {
            using (var gpu = new WallDiffuseGpuFixture((x, y) => (byte)(x == 10 ? 192 : 128)))
            {
                gpu.Shape = shape; gpu.Forward = new Vector2(1, -.5f).normalized;
                gpu.Dispatch(false); var direct = gpu.Pixels(); gpu.Dispatch(true); var reflected = gpu.Pixels();
                float surfaceIncrement = 0;
                for (int y = 0; y < WallDiffuseGpuFixture.Size; y++)
                {
                    int surface = y * WallDiffuseGpuFixture.Size + 40;
                    surfaceIncrement += Mathf.Max(0, reflected[surface].r - direct[surface].r);
                    Assert.That(reflected[y * WallDiffuseGpuFixture.Size + 48].r, Is.Zero);
                }
                Assert.That(surfaceIncrement, Is.GreaterThan(0));
            }
        }

        private static byte FloorAndSlope(int x, int y)
        {
            int top = x < 10 ? 16 : x >= 14 ? 12 : 25 - x;
            if (x >= 10 && x < 14 && y == top) return 193;
            return (byte)(y >= top + (x >= 10 && x < 14 ? 1 : 0) ? 192 : 128);
        }
    }
}
