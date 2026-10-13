using System;
using System.Threading;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Lighting;
using DarkNights.Core.ViewData;
using DarkNights.View.Lighting;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>受光准备失效合同的单元回归；核对真正改变入射的输入必重算、纯合成参数可复用，不改变作者地形或玩家状态。</summary>
    public sealed class WallDiffusePreparationTests
    {
        private CaveVisualSource source;
        private GameObject parent;
        private CaveTerrainStyle style;
        private WorldDescriptor descriptor;
        private WallDiffusePreparationCache cache;
        private readonly Vector4[] positions = new Vector4[16], directions = new Vector4[16], mouths = new Vector4[16], colors = new Vector4[16], shadows = new Vector4[16];
        private readonly LightBounceRules[] rules = new LightBounceRules[16];
        private readonly RectInt bounds = new RectInt(0, 0, 32, 32);
        private LightBoundarySnapshot boundary;

        [OneTimeSetUp]
        public void CreateSource()
        {
            const string root = "Assets/DarkNights/Res/Terrain/StrataCave/";
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(root + "MapDefinition.asset");
            style = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(root + "Style.asset")); style.ProceduralRock = false;
            var blueprint = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(root + "ReferenceChamber.asset").ReadBlueprint();
            var tiles = definition.LoadGameplayCatalog().Tiles;
            descriptor = definition.Describe(new WorldIdentity(StableGuid.Parse(Guid.NewGuid().ToString("N")), 1), 0);
            parent = new GameObject("Pruning input contract");
            source = new CaveVisualSource(new TerrainBlueprintSource(blueprint, tiles), style, tiles, parent.transform,
                new DarkNights.Core.Config.Terrain.BackgroundBakeDescriptor(descriptor.World.WorldId.ToString(), blueprint.Settings.Seed, blueprint.CopyMaterials(), blueprint.CopyShapes()));
            boundary = LightBoundaryBuilder.Build(new LightGeometrySnapshot(2, 2, new byte[] { 128, 192, 128, 192 }), null);
        }

        [SetUp]
        public void FreezeInputs()
        {
            cache = new WallDiffusePreparationCache();
            positions[0] = new Vector4(7, 12, 14, .7f); directions[0] = new Vector4(1, 0, 2, .5f);
            mouths[0] = new Vector4(.2f, 0, 0, 0); shadows[0] = new Vector4(.22f, .04f, 0, 0); colors[0] = new Vector4(1, .8f, .4f, 1.35f);
            rules[0] = new LightBounceRules(LightBounceMode.Continuous);
            cache.Remember(source, boundary, bounds, positions, directions, mouths, colors, shadows, rules, 1, 0);
            Assert.That(Matches(), Is.True);
        }

        private bool Matches() => cache.Matches(source, boundary, bounds, positions, directions, mouths, colors, shadows, rules, 1, 0);

        [TestCase("position")] [TestCase("subpixel")] [TestCase("range")] [TestCase("cone")]
        [TestCase("direction")] [TestCase("aperture")] [TestCase("softness")] [TestCase("feather")] [TestCase("intensity")]
        public void IncidentInputsInvalidatePreparation(string input)
        {
            switch (input)
            {
                case "position": positions[0].y += 1; break;
                case "subpixel": positions[0].x += .000001f; break;
                case "range": positions[0].z += 1; break;
                case "cone": positions[0].w += .01f; break;
                case "direction": directions[0].y += .01f; break;
                case "aperture": mouths[0].x += .01f; break;
                case "softness": shadows[0].x += .01f; break;
                case "feather": shadows[0].y += .01f; break;
                case "intensity": colors[0].w += .01f; break;
            }
            Assert.That(Matches(), Is.False);
        }

        [TestCase("near")] [TestCase("far")] [TestCase("reach")] [TestCase("matching")] [TestCase("corner")] [TestCase("off")]
        public void ProfileAndSelectionRulesInvalidatePreparation(string input)
        {
            rules[0] = new LightBounceRules(input == "off" ? LightBounceMode.Off : LightBounceMode.Continuous,
                diffuseNearDistance: input == "near" ? 2 : 1.5f, diffuseFarDistance: input == "far" ? 7 : 6,
                diffuseMaxReach: input == "reach" ? 4 : 3.2f, diffuseMatchIncident: input == "matching",
                diffuseCornerRadius: input == "corner" ? 0 : .8f);
            Assert.That(Matches(), Is.False);
        }

        [Test]
        public void CompositionOnlyChangesReuseProfilesWithoutAllocatingASecondModel()
        {
            rules[0] = new LightBounceRules(LightBounceMode.Continuous, diffuseShape: WallDiffuseShape.SoftBlob,
                diffuseGain: 2, diffuseLateralSpread: 2, diffuseVisibilityProbes: 4);
            colors[0].x = .4f; directions[0].z = 3; directions[0].w = .75f;
            Assert.That(Matches(), Is.True);
            rules[0] = new LightBounceRules(LightBounceMode.Continuous, diffuseGain: 0);
            Assert.That(Matches(), Is.False, "零增益改变激活状态，不能复用旧受光组。");
        }

        [Test]
        public void BoundaryInstallationRegionCountAndBenchmarkInvalidatePreparation()
        {
            Assert.That(cache.Matches(source, null, bounds, positions, directions, mouths, colors, shadows, rules, 1, 0), Is.False);
            Assert.That(cache.Matches(source, boundary, new RectInt(1, 0, 32, 32), positions, directions, mouths, colors, shadows, rules, 1, 0), Is.False);
            Assert.That(cache.Matches(source, boundary, bounds, positions, directions, mouths, colors, shadows, rules, 0, 0), Is.False);
            Assert.That(cache.Matches(source, boundary, bounds, positions, directions, mouths, colors, shadows, rules, 1, 1), Is.False);
        }

        [Test]
        public void GeometryRevisionInvalidatesEvenBeforeNewBoundaryArrives()
        {
            source.LoadAsync(descriptor, new ChunkCoord(0, -1), CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(Matches(), Is.False);
        }

        [OneTimeTearDown]
        public void ReleaseSource()
        {
            source?.Dispose(); UnityEngine.Object.DestroyImmediate(parent); UnityEngine.Object.DestroyImmediate(style);
        }
    }
}
