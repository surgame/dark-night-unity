using System;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Editor.Terrain;
using DarkNights.View.Expedition;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>松林主配方的地表合同；检查露天空域、坡面根部、密度和确定性，仅使用临时布局，不改正式资源。</summary>
    public sealed class SurfaceForestTests
    {
        private static BackgroundBakeDescriptor Ground(Action<byte[]> edit = null)
        {
            var cells = new byte[320 * 192];
            for (int x = 0; x < 320; x++) cells[40 * 320 + x] = 1;
            edit?.Invoke(cells);
            return new BackgroundBakeDescriptor("00000000000000000000000000000001",
                "FOREST-SUPPORT", cells, new byte[cells.Length]);
        }

        private static SurfaceSceneryPlacement Tree(int row = 40) => new SurfaceSceneryPlacement
        {
            Column = 100, Row = row, Pixels = new RectInt(0, 0, 32, 64), Scale = 2
        };

        [Test]
        public void ExposedSurfaceAcceptsTreesAndBuriedFloorIsRejected()
        {
            Assert.That(SurfaceSceneryLayout.IsOutdoor(Tree(), Ground()), Is.True);
            var cave = Ground(cells => cells[52 * 320 + 100] = 1);
            Assert.That(SurfaceSceneryLayout.IsOutdoor(Tree(52), cave), Is.False);
        }

        [Test]
        public void TreeCanopyRejectsNarrowSkyShaftBesideRoof()
        {
            var shaft = Ground(cells => cells[32 * 320 + 102] = 1);
            Assert.That(SurfaceSceneryLayout.IsOutdoor(Tree(), shaft), Is.False);
        }

        [Test]
        public void GentleSlopeRemainsOutdoorWithoutRequiringFlatNeighboringCells()
        {
            var slope = Ground(cells =>
            {
                cells[40 * 320 + 101] = 0;
                cells[41 * 320 + 101] = 1;
            });
            Assert.That(SurfaceSceneryLayout.IsOutdoor(Tree(), slope), Is.True);
        }

        [Test]
        public void InvalidBoundaryAndChangedShapeAreRejected()
        {
            var tree = Tree(0);
            Assert.That(SurfaceSceneryLayout.IsOutdoor(tree, Ground()), Is.False);
            tree = Tree(); tree.Column = 0;
            Assert.That(SurfaceSceneryLayout.IsOutdoor(tree, Ground()), Is.False);
            tree = Tree(); tree.Shape = 1;
            Assert.That(SurfaceSceneryLayout.IsOutdoor(tree, Ground()), Is.False);
        }

        [Test]
        public void MainForestBuildIsDenseOutdoorAndDeterministic()
        {
            var input = new TerrainGenerationSettings
            {
                Seed = "STRATA-0922", Surface = "rolling",
                ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile,
                Layout = TerrainGenerationSettings.ConnectedRooms,
                Structure = new CaveLayoutSettings(),
                Minerals = new TerrainMineralSettings { Algorithm = TerrainMineralSettings.RegionalVeins },
                Relief = new SurfaceReliefSettings { Enabled = true }
            };
            var planet = new PlanetDefinition("forest", "松林", "", true, input.Seed,
                36, 40, 32, 176, 240, 448, .8f, 120, 110, "none", "#060C20", "#243B55");
            var map = PlanetTerrainGenerator.GenerateCandidate(planet, input.Seed,
                "00000000000000000000000000000001", input);
            var style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(
                "Assets/DarkNights/Res/Terrain/StrataCave/Style.asset");
            var layout = ScriptableObject.CreateInstance<SurfaceSceneryLayout>();
            try
            {
                var random = UnityEngine.Random.state;
                layout.TerrainKey = SurfaceSceneryLayout.Key(map.Background);
                layout.Placements = SurfaceSceneryAuthoring.Build(map, planet, layout, style);
                SurfaceSceneryAuthoring.Validate(layout, map);
                Assert.That(layout.Placements.Count(p => p.Id.StartsWith("tree-", StringComparison.Ordinal)), Is.GreaterThan(20));
                Assert.That(layout.Placements.All(p => SurfaceSceneryLayout.IsOutdoor(p, map.Background)), Is.True);
                var repeated = SurfaceSceneryAuthoring.Build(map, planet, layout, style);
                CollectionAssert.AreEqual(layout.Placements.Select(p => p.Identity), repeated.Select(p => p.Identity));
                Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
            }
            finally { UnityEngine.Object.DestroyImmediate(layout); }
        }
    }
}
