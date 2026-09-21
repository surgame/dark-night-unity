using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>天然洞穴实验的生成合同；验证隐藏连通图、可见空腔、保护边界与确定性，不代替真实角色通行验收。</summary>
    public sealed class CaveExplorationTests
    {
        [Test]
        public void HundredSeedsHaveConnectedHiddenGraphsAndBuriedPassages()
        {
            var signatures = new HashSet<string>();
            for (int seed = 0; seed < 100; seed++)
            {
                var settings = new TerrainGenerationSettings
                { Seed = "CAVE-CHECK-" + seed, ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile };
                var map = TerrainGenerator.Generate(settings);
                Assert.That(map.Rooms.Count, Is.InRange(10, 13));
                Assert.That(map.Passages.Count, Is.GreaterThanOrEqualTo(map.Rooms.Count));
                Assert.That(map.SoftRockCount, Is.GreaterThan(0));
                Assert.That(map.Passages.Count(e => e.Kind == CavePassageKind.Open), Is.GreaterThanOrEqualTo(2));
                Assert.That(map.Rooms.All(r => r.Width <= 25 && r.Height <= 14), Is.True, settings.Seed);
                Assert.That(map.Passages.All(p => p.Radius == 2), Is.True, settings.Seed);
                Assert.That(map.Rooms.Max(r => r.X) - map.Rooms.Min(r => r.X), Is.LessThanOrEqualTo(140), settings.Seed);
                Assert.That(map.Rooms.Max(r => r.Y) - map.Rooms.Min(r => r.Y), Is.LessThanOrEqualTo(56), settings.Seed);
                var reached = new HashSet<int> { 0 };
                for (int n = 0; n < map.Rooms.Count; n++) foreach (var edge in map.Passages)
                {
                    if (reached.Contains(edge.From)) reached.Add(edge.To);
                    if (reached.Contains(edge.To)) reached.Add(edge.From);
                }
                Assert.That(reached.Count, Is.EqualTo(map.Rooms.Count));
                foreach (var room in map.Rooms) Assert.That(map.MaterialAt(room.X, room.Y), Is.Zero);
                for (int x = 0; x < map.Width; x++)
                {
                    Assert.That(map.IsProtected(x, map.Height - 1), Is.True);
                    Assert.That(map.MaterialAt(x, map.Height - 1), Is.EqualTo(8));
                }
                signatures.Add(string.Join(";", map.Passages.Select(e => e.From + ":" + e.To + ":" + e.Kind +
                    ":" + e.BendX + ":" + e.BendY)));
                CollectionAssert.AreEqual(map.CopyMaterials(), TerrainGenerator.Generate(settings).CopyMaterials());
            }
            Assert.That(signatures.Count, Is.GreaterThan(90));
        }

        [Test]
        public void BlueprintFreezesInputsAndExperimentalProfileKeepsLegacyGeneratorSeparate()
        {
            var input = new TerrainGenerationSettings
            { Seed = "CAVE-FREEZE", ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile };
            var map = TerrainGenerator.Generate(input);
            input.Seed = "changed";
            var copy = map.CopyMaterials(); copy[0] = 0;
            Assert.That(map.Settings.Seed, Is.EqualTo("CAVE-FREEZE"));
            Assert.That(map.MaterialAt(0, 0), Is.EqualTo(8));
            var reference = TerrainGenerator.Generate(input.AsReferenceProfile());
            Assert.That(reference.Rooms.Count, Is.EqualTo(8));
            Assert.That(reference.Passages, Is.Empty);
        }

        [Test]
        public void CompactAndLegacyPresetsAreValidatedAndProduceDifferentScales()
        {
            var compact = new TerrainGenerationSettings
            { Seed = "CAVE-SCALE", ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile };
            var tight = TerrainGenerator.Generate(compact);
            compact.UseLegacyCaveScale(); var legacy = TerrainGenerator.Generate(compact);
            Assert.That(tight.Rooms.Max(r => r.Width), Is.LessThan(legacy.Rooms.Max(r => r.Width)));
            Assert.That(tight.Rooms.Max(r => r.Height), Is.LessThan(legacy.Rooms.Max(r => r.Height)));
            Assert.That(tight.Passages.All(p => p.Radius == 2), Is.True);
            Assert.That(legacy.Passages.All(p => p.Radius == 3), Is.True);
            compact.CavePassageRadius = 1;
            Assert.Throws<System.ArgumentOutOfRangeException>(() => TerrainGenerator.Generate(compact));
        }
    }
}
