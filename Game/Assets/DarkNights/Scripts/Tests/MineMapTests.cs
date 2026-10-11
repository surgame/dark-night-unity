using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Terrain;
using DarkNights.Runtime.Objects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>作者矿洞接入的有限合同检查；使用真实导入资源和正式星球流水线，覆盖深复制、接缝、矿层与背景。</summary>
    public sealed class MineMapTests
    {
        private const string Root = "Assets/DarkNights/Res/Terrain/MineMaps/";
        private const string World = "00000000000000000000000000000001";

        [TestCase("rift-expanded")]
        [TestCase("karst-expanded")]
        [TestCase("folded-expanded")]
        [TestCase("shaft-loop-expanded")]
        [TestCase("vein-maze-expanded")]
        public void ImportedMapsUseFormalGeometryAndMineralPipeline(string id)
        {
            var asset = AssetDatabase.LoadAssetAtPath<MineMapAsset>(Root + id + ".minemap");
            Assert.That(asset, Is.Not.Null);
            var definition = asset.Freeze();
            var settings = new TerrainGenerationSettings
            { ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile, MineMap = definition };
            var source = TerrainGenerator.GenerateCave(settings, "MINE-CANDIDATE");
            var planet = new PlanetDefinition("grey-pine", "灰松星", seed: "MINE-CANDIDATE");
            var first = PlanetTerrainGenerator.GenerateCandidate(planet, "MINE-CANDIDATE", World, settings);
            var repeat = PlanetTerrainGenerator.GenerateCandidate(planet, "MINE-CANDIDATE", World, settings);
            Assert.That(first.Count, Is.EqualTo(320 * 192));
            CollectionAssert.AreEqual(first.CopyMaterials(), repeat.CopyMaterials());
            CollectionAssert.AreEqual(first.CopyShapes(), first.Background.CopyShapes());
            CollectionAssert.AreEqual(first.CopyMaterials(), first.Background.CopyMaterials());
            Assert.That(first.Deposits.Count, Is.GreaterThan(definition.BaseOres.Length));
            Assert.That(first.Deposits.Any(d => d.MineralKind == "gold"), Is.True);
            Assert.That(first.Rooms.Count, Is.EqualTo(definition.Rooms.Length));
            Assert.That(first.Material(planet.DockColumn, planet.DockRow), Is.Not.Zero);
            Assert.That(first.Material(planet.DockColumn, planet.DockRow - 1), Is.Zero);
            Assert.That(first.CopyProtection()[planet.DockRow * 320 + planet.DockColumn], Is.True);
            int maxX = 0, maxY = 0;
            for (int y = definition.OffsetY + definition.BaseHeight; y < definition.OffsetY + definition.Height; y++)
                for (int x = definition.OffsetX; x < definition.OffsetX + definition.Width; x++)
                    if (first.Material(x, y) == 0) { maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
            Assert.That(maxX, Is.GreaterThanOrEqualTo(definition.OffsetX + 240));
            Assert.That(maxY, Is.GreaterThanOrEqualTo(definition.OffsetY + 138));
            byte[] baseline = definition.DecodeBase();
            for (int y = 0; y < 78; y++) for (int x = 0; x < 132; x++)
            {
                if (definition.AllowsSeam(x, y)) continue;
                Assert.That(source.MaterialAt(x + 40, y + 32), Is.EqualTo(baseline[y * 132 + x] == 0 ? 0 : 2));
                Assert.That(source.IsSoftRock(x + 40, y + 32), Is.EqualTo(baseline[y * 132 + x] == 2));
            }
            foreach (var deposit in first.Deposits)
                Assert.That(deposit.Cells.Sum(c => c.Capacity), Is.EqualTo(deposit.Capacity));
        }

        [Test]
        public void FreezeCopiesNestedAuthorDataAndRejectsInvalidInput()
        {
            var asset = AssetDatabase.LoadAssetAtPath<MineMapAsset>(Root + "folded-expanded.minemap");
            var frozen = asset.Freeze(); var edited = frozen.CopyValidated();
            edited.Paths[0].Points[0]++;
            Assert.That(frozen.Paths[0].Points[0], Is.Not.EqualTo(edited.Paths[0].Points[0]));
            edited = frozen.CopyValidated(); edited.Rooms[0].Id = edited.Rooms[1].Id;
            Assert.Throws<ArgumentException>(() => edited.CopyValidated());
            edited = frozen.CopyValidated(); edited.BaseRuns = "1:1";
            Assert.Throws<ArgumentException>(() => edited.CopyValidated());
            edited = frozen.CopyValidated(); edited.Shafts[0].Bottom = edited.Shafts[0].Top + 15;
            Assert.Throws<ArgumentException>(() => edited.CopyValidated());
            edited = frozen.CopyValidated(); edited.Paths[0].From = "missing";
            Assert.Throws<ArgumentException>(() => edited.CopyValidated());
            edited = frozen.CopyValidated(); edited.Seams = Array.Empty<int>();
            var settings = new TerrainGenerationSettings
            { ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile, MineMap = edited };
            Assert.Throws<InvalidOperationException>(() => MineMapGenerator.Generate(settings));
        }

        [Test]
        public void FormalConfigurationAndDraftKeepAssetIdentityAndFingerprint()
        {
            var asset = AssetDatabase.LoadAssetAtPath<MineMapAsset>(Root + "folded-expanded.minemap");
            var config = new ExpeditionFlowConfig(); string before = config.Fingerprint();
            config.MineMap = asset; string selected = config.Fingerprint();
            Assert.That(selected, Is.Not.EqualTo(before));
            var frozen = config.FreezeCaveMap();
            Assert.That(frozen.MineMap.Id, Is.EqualTo("folded-expanded"));
            Assert.That(JsonUtility.ToJson(frozen), Does.Not.Contain("MineMap"));
            Assert.That(JsonUtility.FromJson<TerrainGenerationSettings>(JsonUtility.ToJson(frozen)).MineMap, Is.Null);
            config.MineMap = null;
            Assert.That(frozen.MineMap.Id, Is.EqualTo("folded-expanded"));
            Assert.That(config.FreezeCaveMap().MineMap, Is.Null);
            Assert.That(config.Fingerprint(), Is.EqualTo(before));
        }

        [Test]
        public void PreviewIdentityChangesWithSelectedMapAndAuthorGeometry()
        {
            var copy = ScriptableObject.CreateInstance<MineMapAsset>();
            try
            {
                using (var preview = new DarkNights.Editor.Terrain.TerrainGenerationPreview())
                {
                    preview.Draft.Config.MineMap = null; string random = preview.Identity;
                    var asset = AssetDatabase.LoadAssetAtPath<MineMapAsset>(Root + "folded-expanded.minemap");
                    preview.Draft.Config.MineMap = asset;
                    Assert.That(preview.Identity, Is.Not.EqualTo(random));
                    copy.Definition = asset.Freeze(); preview.Draft.Config.MineMap = copy;
                    string before = preview.Identity; copy.Definition.Paths[0].Points[2]++;
                    Assert.That(preview.Identity, Is.Not.EqualTo(before));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
    }
}
