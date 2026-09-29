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
    /// <summary>完整生成步骤的独立合同验收；从最终格子重新洪泛比较空腔，覆盖冻结、阶段、诊断和矿床锚点。</summary>
    public sealed class TerrainGenerationPipelineTests
    {
        private const string World = "00000000000000000000000000000001";
        private const int W = 320, H = 192;
        private static TerrainGenerationPipeline Empty => new TerrainGenerationPipeline(Array.Empty<ITerrainGenerationModifier>());
        public static IEnumerable<string> Seeds => new[] { "STRATA-0922", "OPEN-CAVE-0" }
            .Concat(Enumerable.Range(0, 100).Select(i => "OPEN-CAVE-" + i)).Distinct();

        [TestCaseSource(nameof(Seeds))]
        public void FixedSeedPreservesEveryOriginalCavityAndRoom(string seed)
        {
            var planet = new PlanetDefinition("fixed", "固定种子", seed: seed);
            var before = PlanetTerrainGenerator.Generate(planet, seed, World, pipeline: Empty);
            var after = PlanetTerrainGenerator.Generate(planet, seed, World);
            int[] original = Components(before.CopyMaterials()), current = Components(after.CopyMaterials());
            var destinations = new Dictionary<int, int>();
            for (int i = 0; i < original.Length; i++)
            {
                if (original[i] == 0 || current[i] == 0) continue;
                if (destinations.TryGetValue(original[i], out int destination))
                    Assert.That(current[i], Is.EqualTo(destination), seed + " split at " + i % W + "," + i / W);
                else destinations.Add(original[i], current[i]);
            }
            int entrance = current[(planet.DockRow - 1) * W + planet.DockColumn];
            Assert.That(entrance, Is.GreaterThan(0));
            foreach (var room in after.Rooms)
                Assert.That(current[room.Y * W + room.X], Is.EqualTo(entrance), seed + " room " + room.X + "," + room.Y);
            CollectionAssert.AreEqual(after.CopyMaterials(), after.Background.CopyMaterials());
            CollectionAssert.AreEqual(after.CopyShapes(), after.Background.CopyShapes());
        }

        [Test]
        public void StageBarriersOrderingAndFlagOnlyDiagnosticsUseFinalCells()
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
            CollectionAssert.AreEqual(new[] { "natural", "sky", "dock", "ordered", "last" }, called);
            Assert.That(report.Last().ChangedCells, Is.EqualTo(1));
            Assert.That(report.Last().MinX, Is.EqualTo(110));
            Assert.That(map.CopyShapes()[80 * W + 110], Is.Zero);
            CollectionAssert.AreEqual(map.CopyMaterials(), map.Background.CopyMaterials());
        }

        [Test]
        public void FrozenModifiersIgnoreLaterAuthorEditsAndDisabledIsEmpty()
        {
            var config = new ExpeditionFlowConfig(); var frozen = config.FreezeModifiers();
            var planet = config.PreviewPlanet();
            var first = PlanetTerrainGenerator.Generate(planet, "STRATA-0922", World, pipeline: frozen);
            ((EntranceWalkwayModifierConfig)config.Modifiers[0]).Enabled = false;
            var repeat = PlanetTerrainGenerator.Generate(planet, "STRATA-0922", World, pipeline: frozen);
            CollectionAssert.AreEqual(first.CopyMaterials(), repeat.CopyMaterials());
            var disabled = PlanetTerrainGenerator.Generate(planet, "STRATA-0922", World, pipeline: config.FreezeModifiers());
            var empty = PlanetTerrainGenerator.Generate(planet, "STRATA-0922", World, pipeline: Empty);
            CollectionAssert.AreEqual(disabled.CopyMaterials(), empty.CopyMaterials());
            Assert.That(first.CopyMaterials().SequenceEqual(empty.CopyMaterials()), Is.False);
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

        private static int[] Components(byte[] cells)
        {
            var labels = new int[cells.Length]; int label = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != 0 || labels[i] != 0) continue;
                var queue = new Queue<int>(); labels[i] = ++label; queue.Enqueue(i);
                while (queue.Count != 0)
                {
                    int at = queue.Dequeue(), x = at % W, y = at / W;
                    foreach (int next in new[] { x == 0 ? -1 : at - 1, x == W - 1 ? -1 : at + 1,
                        y == 0 ? -1 : at - W, y == H - 1 ? -1 : at + W })
                        if (next >= 0 && cells[next] == 0 && labels[next] == 0)
                        { labels[next] = label; queue.Enqueue(next); }
                }
            }
            return labels;
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
