using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>冻结参考生成向量与参数边界回归；不根据当前 Unity 结果重生成参考答案。</summary>
    public sealed class TerrainGenerationTests
    {
        [Test]
        public void HtmlVectorsMatchAllTwentyFourMaps()
        {
            var document = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "../../tools/terrain-reference/generation-vectors.json")));
            using var sha = SHA256.Create();
            foreach (var v in document["vectors"])
            {
                var settings = new TerrainGenerationSettings { Seed = (string)v["seed"], Surface = (string)v["surface"], OrganicCaves = (string)v["caves"] == "organic" }.AsReferenceProfile();
                var map = TerrainGenerator.Generate(settings);
                string hash = BitConverter.ToString(sha.ComputeHash(map.CopyMaterials())).Replace("-", "").ToLowerInvariant();
                Assert.That(hash, Is.EqualTo((string)v["sha256"]), settings.Seed + "/" + settings.Surface + "/" + settings.OrganicCaves);
            }
        }

        [Test]
        public void GameplayProfileKeepsScatteredOreRoomDriven()
        {
            var settings = new TerrainGenerationSettings { Seed = "MAP-PLAN-M1", Surface = "rolling", OrganicCaves = true };
            var first = TerrainGenerator.Generate(settings);
            var second = TerrainGenerator.Generate(settings);
            int scattered = 0;
            for (int y = 0; y < first.Height; y++) for (int x = 0; x < first.Width; x++)
                if (first.MaterialAt(x, y) >= 4 && first.MaterialAt(x, y) <= 6) scattered++;
            Assert.That(first.CopyMaterials(), Is.EqualTo(second.CopyMaterials()));
            Assert.That(first.Deposits.Count, Is.EqualTo(11));
            Assert.That(first.SoftRockCount, Is.GreaterThan(0));
            Assert.That(scattered, Is.GreaterThan(0).And.LessThan(220));
            Assert.That(first.Rooms.Count(r => (r.Features & TerrainRoomFeature.MineralDeposit) != 0), Is.EqualTo(3));
        }
        [Test]
        public void GenerationIsFrozenAndProtectsSupport()
        {
            var settings = new TerrainGenerationSettings(); var map = TerrainGenerator.Generate(settings);
            settings.Seed = "changed"; var cells = map.CopyMaterials(); cells[72 * map.Width + 62] = 0;
            Assert.That(map.Settings.Seed, Is.EqualTo("GREYPINE-1616"));
            Assert.That(map.MaterialAt(62, 72), Is.EqualTo(7));
            Assert.That(map.IsProtected(62, 73), Is.True);
            Assert.That(map.IsProtected(0, 20), Is.True);
            Assert.That(map.Rooms.Count, Is.EqualTo(8));
        }
        [TestCase(double.NaN, 1)]
        [TestCase(double.PositiveInfinity, 1)]
        [TestCase(1, 0)]
        public void InvalidSettingsFailBeforeGenerating(double amplitude, double ore)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TerrainGenerator.Generate(new TerrainGenerationSettings { Amplitude = amplitude, OreDensity = ore }));
        }
    }
}
