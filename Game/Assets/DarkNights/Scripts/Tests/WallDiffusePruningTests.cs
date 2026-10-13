using System;
using System.Collections.Generic;
using System.IO;
using DarkNights.Core.Config;
using NUnit.Framework;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>冻结优化前 Shader 与正式候选的独立 GPU 像素对照；覆盖坡形、两种光形及参数边界，记录最大误差，不更新冻结基线或作者资产。</summary>
    public sealed class WallDiffusePruningTests
    {
        public const string ReferenceShader = "Assets/DarkNights/Scripts/Tests/LightingPruningBaseline/ExplorationLighting.compute";
        private readonly List<object> comparisons = new List<object>();

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)]
        public void EverySlopeAndBothShapesMatchFrozenRendering(int slope)
        {
            Func<int, int, byte> cell = (x, y) => (byte)(x == 10 ? 192 | slope : x > 14 ? 0 : y >= 19 ? 192 : 128);
            foreach (var shape in new[] { WallDiffuseShape.WallBand, WallDiffuseShape.SoftBlob })
                Compare(cell, shape, slope, gpu =>
                {
                    gpu.Count = slope % 3 == 0 ? 4 : 1;
                    gpu.Forward = new Vector2(1, slope % 2 == 0 ? .35f : -.35f).normalized;
                    gpu.Softness = .22f; gpu.Aperture = .4835f;
                    gpu.MatchIncident = slope % 2 == 0;
                });
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
        public void CornerAndParameterBoundariesMatchFrozenRendering(int variant)
        {
            foreach (var shape in new[] { WallDiffuseShape.WallBand, WallDiffuseShape.SoftBlob })
                Compare(Corner, shape, variant, gpu =>
                {
                    gpu.OriginX = 9; gpu.OriginY = 13; gpu.Forward = new Vector2(1, 3).normalized;
                    gpu.NearRange = 2.2f; gpu.Intensity = variant == 0 ? 0 : 2.78f;
                    gpu.Returns[0] = new LightBounceRules(LightBounceMode.Continuous,
                        diffuseShape: shape, diffuseGain: variant == 1 ? 0 : variant == 2 ? 2 : 1,
                        diffuseMatchIncident: variant % 2 == 0, diffuseCornerRadius: variant == 3 ? 0 : variant == 4 ? 2 : .8f,
                        diffuseNearDistance: variant == 5 ? 0 : 1.5f, diffuseFarDistance: variant == 6 ? 14 : 6,
                        diffuseMaxReach: variant == 7 ? 0 : variant == 8 ? 6 : 3.2f,
                        diffuseLateralSpread: variant == 9 ? 2 : 0, diffuseVisibilityProbes: variant == 10 ? 4 : 2);
                });
        }

        [TestCase(WallDiffuseShape.WallBand)] [TestCase(WallDiffuseShape.SoftBlob)]
        public void LateralSpreadHasValueOnlyInWallBand(WallDiffuseShape shape)
        {
            using (var gpu = new WallDiffuseGpuFixture((x, y) => (byte)(x >= 10 ? 192 : 128)))
            {
                gpu.Returns[0] = new LightBounceRules(LightBounceMode.Continuous, diffuseShape: shape, diffuseLateralSpread: 0);
                gpu.Dispatch(true); var first = gpu.Pixels();
                gpu.Returns[0] = new LightBounceRules(LightBounceMode.Continuous, diffuseShape: shape, diffuseLateralSpread: 2);
                gpu.Dispatch(true); var next = gpu.Pixels(); float maximum = Maximum(first, next);
                if (shape == WallDiffuseShape.SoftBlob) Assert.That(maximum, Is.Zero);
                else Assert.That(maximum, Is.GreaterThan(.001f));
                comparisons.Add(new { kind = "parameter-value", shape = shape.ToString(), parameter = "DiffuseLateralSpread", maximum });
            }
        }

        [TestCase(WallDiffuseShape.WallBand)] [TestCase(WallDiffuseShape.SoftBlob)]
        public void IncidentMatchingAndProbesHaveValueOnSlopesAndWallSurfaces(WallDiffuseShape shape)
        {
            using (var gpu = new WallDiffuseGpuFixture(Corner))
            {
                gpu.OriginX = 9; gpu.OriginY = 13; gpu.Forward = new Vector2(1, 3).normalized; gpu.Shape = shape;
                gpu.Dispatch(true); var normal = gpu.Pixels();
                gpu.MatchIncident = true; gpu.Dispatch(true); var matched = gpu.Pixels();
                Assert.That(Maximum(normal, matched), Is.GreaterThan(.001f));
                gpu.Probes = 4; gpu.Dispatch(true); var moreProbes = gpu.Pixels();
                Assert.That(Maximum(matched, moreProbes), Is.GreaterThan(.00001f));
                comparisons.Add(new { kind = "parameter-value", shape = shape.ToString(),
                    matchingDifference = Maximum(normal, matched), probeDifference = Maximum(matched, moreProbes) });
            }
        }

        private void Compare(Func<int, int, byte> cells, WallDiffuseShape shape, int variant, Action<WallDiffuseGpuFixture> configure)
        {
            using (var reference = new WallDiffuseGpuFixture(cells, ReferenceShader))
            using (var candidate = new WallDiffuseGpuFixture(cells))
            {
                reference.Shape = shape; candidate.Shape = shape; configure(reference); configure(candidate);
                reference.Dispatch(true); candidate.Dispatch(true);
                var original = reference.Pixels(); var optimized = candidate.Pixels();
                float rgb = Maximum(original, optimized), direction = Maximum(reference.Pixels(true), candidate.Pixels(true));
                comparisons.Add(new { kind = "frozen-pixels", shape = shape.ToString(), variant, maximumRgbError = rgb, maximumDirectionError = direction,
                    pixels = original.Length, tolerance = .00003f });
                Assert.That(rgb, Is.LessThanOrEqualTo(.00003f), "剪枝改变了冻结基线的 RGB。");
                Assert.That(direction, Is.LessThanOrEqualTo(.00003f), "剪枝改变了方向／亮度数据。");
            }
        }

        public static float Maximum(Color[] first, Color[] second)
        {
            float maximum = 0;
            for (int i = 0; i < first.Length; i++)
                for (int channel = 0; channel < 4; channel++) maximum = Mathf.Max(maximum, Mathf.Abs(first[i][channel] - second[i][channel]));
            return maximum;
        }

        public static byte Corner(int x, int y)
        {
            int top = x < 10 ? 16 : x >= 14 ? 12 : 25 - x;
            if (x >= 10 && x < 14 && y == top) return 193;
            return (byte)(y >= top + (x >= 10 && x < 14 ? 1 : 0) ? 192 : 128);
        }

        [OneTimeTearDown]
        public void SaveComparisons()
        {
            string path = Path.GetFullPath("../artifacts/light-pruning-20261013/pixel-comparisons.json");
            if (File.Exists(path)) path = Path.ChangeExtension(path, Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(comparisons, Newtonsoft.Json.Formatting.Indented));
        }
    }
}
