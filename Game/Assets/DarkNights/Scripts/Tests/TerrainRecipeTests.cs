using System;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>统一配方的纯数据回归；覆盖导入不变性、完整阶段与非法组合，不依赖窗口、本机预设或正式资产。</summary>
    public sealed class TerrainRecipeTests
    {
        private static TerrainGenerationSettings Connected() => new TerrainGenerationSettings
        {
            Seed = "RECIPE-CHECK-1", ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile,
            Layout = TerrainGenerationSettings.ConnectedRooms, Structure = new CaveLayoutSettings(),
            Minerals = new TerrainMineralSettings { Algorithm = TerrainMineralSettings.RegionalVeins }, Rules = null
        };

        [Test]
        public void ImportProducesIndependentStandardParameters()
        {
            var input = new TerrainGenerationSettings
            {
                ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile,
                Rules = new CaveRuleSettings { Enabled = true, Road = "roots", ContentSeed = "IMPORT-ORE", VeinCount = 5, Nests = false }
            };
            var frozen = input.CopyValidated();
            Assert.That(frozen.Layout, Is.EqualTo(TerrainGenerationSettings.ConnectedRooms));
            Assert.That(frozen.Structure.Road, Is.EqualTo("roots"));
            Assert.That(frozen.Minerals.Seed, Is.EqualTo("IMPORT-ORE"));
            Assert.That(frozen.Minerals.VeinCount, Is.EqualTo(5));
            Assert.That(frozen.Minerals.Nests, Is.False);
            Assert.That(frozen.Rules, Is.Null);
            Assert.That(input.Layout, Is.Empty);
            Assert.That(input.Rules.Enabled, Is.True);
            frozen.Structure.Road = "cluster"; frozen.Minerals.VeinCount = 9;
            Assert.That(input.Rules.Road, Is.EqualTo("roots"));
            Assert.That(input.Rules.VeinCount, Is.EqualTo(5));
        }

        [Test]
        public void StandardParametersRemainIndependentAfterFreezing()
        {
            var input = Connected();
            var frozen = input.CopyValidated();
            frozen.Structure.RoomScale = .8;
            frozen.Minerals.Seed = "OTHER-ORE";
            Assert.That(input.Structure.RoomScale, Is.EqualTo(1));
            Assert.That(input.Minerals.Seed, Is.EqualTo("ORE-1"));
        }

        [TestCase("version")]
        [TestCase("entrance")]
        [TestCase("content-seed")]
        [TestCase("vein-count")]
        [TestCase("room-scale")]
        public void HistoricalParametersUseStandardValidationAfterImport(string invalid)
        {
            var input = new TerrainGenerationSettings
            {
                ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile,
                Rules = new CaveRuleSettings { Enabled = true }
            };
            if (invalid == "version") input.Rules.Version = 0;
            if (invalid == "entrance") input.Rules.Entrance = "unknown";
            if (invalid == "content-seed") input.Rules.ContentSeed = "";
            if (invalid == "vein-count") input.Rules.VeinCount = 0;
            if (invalid == "room-scale") input.Rules.RoomScale = double.NaN;
            Assert.Throws<ArgumentException>(() => input.CopyValidated());
            Assert.That(input.Layout, Is.Empty, "失败不能改写原输入。");
        }

        [Test]
        public void SerializedHistoricalInputRetainsImportedWallParameters()
        {
            var input = new Runtime.Objects.ExpeditionFlowConfig();
            input.CaveMap = UnityEngine.JsonUtility.FromJson<TerrainGenerationSettings>(
                UnityEngine.JsonUtility.ToJson(input.CaveMap));
            Runtime.Terrain.TerrainAuthoringImport.Normalize(input, 4, false);
            Assert.That(input.CaveMap.Minerals.Algorithm, Is.EqualTo(TerrainMineralSettings.WallVeins));
            Assert.That(input.CaveMap.Minerals.VeinCount, Is.EqualTo(4));
            Assert.That(input.CaveMap.Minerals.Nests, Is.False);
        }

        [Test]
        public void StandardRecipeIgnoresHistoricalImportDefaults()
        {
            var input = new Runtime.Objects.ExpeditionFlowConfig { CaveMap = Connected() };
            input.CaveMap.Minerals.VeinCount = 5;
            Runtime.Terrain.TerrainAuthoringImport.Normalize(input, 2, false);
            Assert.That(input.CaveMap.Minerals.Algorithm, Is.EqualTo(TerrainMineralSettings.RegionalVeins));
            Assert.That(input.CaveMap.Minerals.VeinCount, Is.EqualTo(5));
            Assert.That(input.CaveMap.Minerals.Nests, Is.True);
        }

        [Test]
        public void RegionalMineralsRejectLayoutWithoutRegions()
        {
            var input = Connected(); input.Layout = TerrainGenerationSettings.NaturalLayout;
            Assert.Throws<ArgumentException>(() => input.CopyValidated());
        }

        [Test]
        public void RemovedTemplateLayoutIsRejected()
        {
            var input = Connected(); input.Layout = "terrain-template";
            input.Minerals.Algorithm = TerrainMineralSettings.WallVeins;
            Assert.Throws<ArgumentException>(() => input.CopyValidated());
        }

        [Test]
        public void UnknownAlgorithmsAreRejected()
        {
            var input = Connected(); input.Layout = "unsupported";
            Assert.Throws<ArgumentException>(() => input.CopyValidated());
            input.Layout = TerrainGenerationSettings.ConnectedRooms; input.Minerals.Algorithm = "unsupported";
            Assert.Throws<ArgumentException>(() => input.CopyValidated());
        }

        [Test]
        public void FullPipelineExecutesChosenContentAndPreservesSpace()
        {
            var input = Connected();
            var planet = new PlanetDefinition("test", "test", "", true, input.Seed,
                36, 40, 32, 176, 240, 448, .8f, 120, 110, "none", "#060C20", "#243B55");
            var regional = PlanetTerrainGenerator.GenerateCandidate(planet, input.Seed, "00000000000000000000000000000001", input);
            var repeated = PlanetTerrainGenerator.GenerateCandidate(planet, input.Seed, "00000000000000000000000000000001", input);
            Assert.That(regional.Deposits.Count, Is.GreaterThan(0));
            Assert.That(regional.Deposits.All(x => x.Id.StartsWith("rule-r", StringComparison.Ordinal)), Is.True);
            Assert.That(repeated.Background.ReferenceHash, Is.EqualTo(regional.Background.ReferenceHash));
            Assert.That(repeated.Deposits.Select(x => x.Id + ":" + x.Capacity),
                Is.EqualTo(regional.Deposits.Select(x => x.Id + ":" + x.Capacity)));
            input.Minerals.Algorithm = TerrainMineralSettings.BaseDeposits;
            var empty = PlanetTerrainGenerator.GenerateCandidate(planet, input.Seed, "00000000000000000000000000000001", input);
            Assert.That(empty.Deposits, Is.Empty);
            Assert.That(empty.Rooms.Select(x => x.X + ":" + x.Y),
                Is.EqualTo(regional.Rooms.Select(x => x.X + ":" + x.Y)));
        }
    }
}
