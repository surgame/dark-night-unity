using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>完整生成步骤的合同验收；覆盖天然轮廓保留、旧步道退场、阶段屏障、诊断和矿床锚点。</summary>
    public sealed class TerrainGenerationPipelineTests
    {
        private const string World = "00000000000000000000000000000001";
        private static TerrainGenerationPipeline Empty => new TerrainGenerationPipeline(Array.Empty<ITerrainGenerationModifier>());
        public static IEnumerable<string> Seeds => new[] { "STRATA-0922", "OPEN-CAVE-0" }
            .Concat(Enumerable.Range(0, 100).Select(i => "OPEN-CAVE-" + i)).Distinct();

        [TestCaseSource(nameof(Seeds))]
        public void DefaultPipelinePreservesNaturalCellsAndGeometry(string seed)
        {
            var planet = new PlanetDefinition("fixed", "固定种子", seed: seed);
            var before = PlanetTerrainGenerator.Generate(planet, seed, World, pipeline: Empty);
            var after = PlanetTerrainGenerator.Generate(planet, seed, World);
            CollectionAssert.AreEqual(before.CopyMaterials(), after.CopyMaterials(), seed);
            CollectionAssert.AreEqual(before.CopyShapes(), after.CopyShapes(), seed);
            CollectionAssert.AreEqual(before.CopySoftRock(), after.CopySoftRock(), seed);
            CollectionAssert.AreEqual(after.CopyMaterials(), after.Background.CopyMaterials());
            CollectionAssert.AreEqual(after.CopyShapes(), after.Background.CopyShapes());
        }

        [Test]
        public void PlanetGenerationIgnoresAdditionalShapeSteps()
        {
            var called = new List<string>(); var report = new List<TerrainModifierDiagnostic>();
            var pipeline = new TerrainGenerationPipeline(new ITerrainGenerationModifier[]
            {
                new Probe("last", TerrainGenerationStage.BeforeGeometry, c => { called.Add("last"); c.SetCell(110, 80, 2, true); }),
                new Probe("natural", TerrainGenerationStage.AfterCave, c => { called.Add("natural"); c.SetCell(110, 10, 2); }),
                new Probe("sky", TerrainGenerationStage.AfterSky, c => { called.Add("sky"); Assert.That(c.MaterialAt(110, 10), Is.Zero); c.SetCell(36, 40, 0); }),
                new Probe("dock", TerrainGenerationStage.AfterDock, c => { called.Add("dock"); Assert.That(c.IsProtected(36, 40), Is.True); c.SetCell(110, 80, 2); }),
                new Probe("ordered", TerrainGenerationStage.AfterDock, c => { called.Add("ordered"); Assert.That(c.MaterialAt(110, 80), Is.EqualTo(2)); })
            });
            var map = PlanetTerrainGenerator.Generate(new PlanetDefinition("test", "测试"), "STRATA-0922", World,
                pipeline: pipeline, report: report.Add);
            Assert.That(called, Is.Empty);
            Assert.That(report, Is.Empty);
            var ordinary = PlanetTerrainGenerator.Generate(new PlanetDefinition("test", "测试"), "STRATA-0922", World);
            CollectionAssert.AreEqual(ordinary.CopyMaterials(), map.CopyMaterials());
            CollectionAssert.AreEqual(ordinary.CopySoftRock(), map.CopySoftRock());
            CollectionAssert.AreEqual(ordinary.CopyShapes(), map.CopyShapes());
            CollectionAssert.AreEqual(map.CopyMaterials(), map.Background.CopyMaterials());
        }

        [Test]
        public void RetiredWalkwayIsIgnoredForDefaultsLegacyAndExplicitSteps()
        {
            var config = new ExpeditionFlowConfig();
            Assert.That(config.Modifiers, Is.Empty);
            var planet = config.PreviewPlanet();
            var empty = PlanetTerrainGenerator.Generate(planet, "STRATA-0922", World, pipeline: Empty).CopyMaterials();
            config.Modifiers.Add(new EntranceWalkwayModifierConfig { Enabled = true, Headroom = -1, MaxCandidates = 0 });
            var pipelines = new List<TerrainGenerationPipeline> { config.FreezeModifiers(),
                new TerrainGenerationPipeline(new ITerrainGenerationModifier[] { new EntranceWalkwayModifier() }) };
            config.ModifierSchemaVersion = 0;
            pipelines.Add(config.FreezeModifiers());
            config.ModifierSchemaVersion = 1; config.Modifiers = null;
            pipelines.Add(config.FreezeModifiers());
            foreach (var pipeline in pipelines)
                CollectionAssert.AreEqual(empty, PlanetTerrainGenerator.Generate(planet, "STRATA-0922", World,
                    pipeline: pipeline).CopyMaterials());
        }

        [Test]
        public void EmptyPipelinePreservesLegalAirDepositAnchors()
        {
            const string seed = "STRATA-0922";
            var source = TerrainGenerator.GenerateCave(new TerrainGenerationSettings
                { ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile }, seed);
            var map = PlanetTerrainGenerator.Generate(new PlanetDefinition("test", "测试"), seed, World, pipeline: Empty);
            var air = source.Deposits.Where(d => source.MaterialAt(d.X, d.Y) == 0).ToArray();
            Assert.That(air.Length, Is.GreaterThan(0));
            foreach (var deposit in air) Assert.That(map.Deposits.Any(d => d.Id == deposit.Id), Is.True, deposit.Id);
        }

        [Test]
        public void InvalidStagesNullStepsAndCancellationFailBeforePublishing()
        {
            Assert.Throws<ArgumentException>(() => new TerrainGenerationPipeline(new ITerrainGenerationModifier[] { null }));
            Assert.Throws<ArgumentException>(() => new TerrainGenerationPipeline(new[] { new Probe("bad", (TerrainGenerationStage)99, _ => { }) }));
            var report = new List<TerrainModifierDiagnostic>();
            Assert.Throws<OperationCanceledException>(() => PlanetTerrainGenerator.GenerateCandidate(
                new PlanetDefinition("test", "测试"), "STRATA-0922", World, new TerrainGenerationSettings(),
                () => true, report: report.Add));
            Assert.That(report, Is.Empty);
        }

        /// <summary>阶段屏障探针；仅记录执行顺序和按测试指定的位置改写格子。</summary>
        private sealed class Probe : ITerrainGenerationModifier
        {
            private readonly Action<TerrainGenerationContext> apply;
            public string StableId { get; }
            public TerrainGenerationStage Stage { get; }
            public Probe(string id, TerrainGenerationStage stage, Action<TerrainGenerationContext> apply)
            { StableId = id; Stage = stage; this.apply = apply; }
            public void Apply(TerrainGenerationContext context) => apply(context);
        }
    }
}
