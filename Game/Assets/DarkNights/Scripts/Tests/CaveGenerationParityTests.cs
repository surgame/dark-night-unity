using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Editor.Terrain;
using DarkNights.Editor;
using DarkNights.Entry.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.View.Terrain;
using GameCore.Objects.Definition;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>正式航程、随机工作台与 Tuner 的最终地图合同；逐格核验并检查实际空腔连通，不以隐藏连接图替代。</summary>
    public sealed class CaveGenerationParityTests
    {
        private static ObjectDefinition Source => AssetDatabase.LoadAssetAtPath<ObjectDefinition>(
            "Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset");
        private static ExpeditionFlowConfig Config => Source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();

        [Test]
        public void TunerAndBothWorkbenchScenesProduceIdenticalFinalCells()
        {
            using var tuner = new TerrainGenerationPreview();
            var expected = tuner.Generate();
            foreach (string path in new[] { TerrainScenePaths.ReferenceChamber, TerrainScenePaths.RandomCave })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var boot = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TerrainDebugBootstrap>(true)).Single();
                    Assert.That(boot.MapAssemblySource, Is.SameAs(Source), path);
                    boot.Settings = Config.FreezeCaveMap();
                    Same(expected, boot.CaptureMapGenerator()());
                    CollectionAssert.AreEqual(expected.CopyMaterials(), expected.Background.CopyMaterials());
                    CollectionAssert.AreEqual(expected.CopyShapes(), expected.Background.CopyShapes());
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
        }

        [UnityTest]
        public IEnumerator ActualJourneyUsesTunerFinalMap() => UniTask.ToCoroutine(() => ActualJourney(false));

        [UnityTest]
        public IEnumerator ActualJourneyUsesDisabledModifierMap() => UniTask.ToCoroutine(() => ActualJourney(true));

        private static async UniTask ActualJourney(bool disable)
        {
            using var tuner = new TerrainGenerationPreview();
            var draft = (ExpeditionFlowDraft)typeof(TerrainGenerationPreview).GetField("draft", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tuner);
            if (disable) ((EntranceWalkwayModifierConfig)draft.Config.Modifiers[0]).Enabled = false;
            var expected = tuner.Generate();
            using var scope = await UnifiedSessionScope.Create();
            var world = JourneyScenario.Create(scope, c =>
            {
                c.CaveMap = Config.FreezeCaveMap();
                c.Planets = JsonUtility.FromJson<ExpeditionFlowConfig>(JsonUtility.ToJson(Config)).Planets;
                c.Modifiers = draft.Config.CopyModifiers();
                c.Planets.First(p => p.Enabled).Seed = expected.Seed;
            });
            using var authority = new SessionAuthority(world); var host = ShipScenario.Connect(authority, 0);
            await JourneyScenario.Descent(authority, world, host);
            var actual = world.Terrain.Capture();
            Same(expected, actual.Blueprint());
            CollectionAssert.AreEqual(expected.Background.CopyMaterials(), actual.Background.CopyMaterials());
            CollectionAssert.AreEqual(expected.Background.CopyShapes(), actual.Background.CopyShapes());
        }

        [Test]
        public void HundredFinalMapsKeepEveryRoomInTheSameOpenCavity()
        {
            var settings = Config.FreezeCaveMap(); var planet = Config.PreviewPlanet();
            for (int seed = 0; seed < 100; seed++)
            {
                var map = PlanetTerrainGenerator.GenerateCandidate(planet, "OPEN-CAVE-" + seed,
                    TerrainGenerationPreview.WorldId, settings);
                Assert.That(map.CopySoftRock().Any(v => v), Is.False);
                var cells = map.CopyMaterials(); var reached = new bool[cells.Length]; var queue = new Queue<int>();
                var first = map.Rooms[0]; int origin = first.Y * 320 + first.X;
                Assert.That(cells[origin], Is.Zero); queue.Enqueue(origin); reached[origin] = true;
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue(), x = i % 320, y = i / 320;
                    foreach (int next in new[] { x > 0 ? i - 1 : -1, x < 319 ? i + 1 : -1,
                        y > 0 ? i - 320 : -1, y < 191 ? i + 320 : -1 })
                        if (next >= 0 && !reached[next] && cells[next] == 0)
                        { reached[next] = true; queue.Enqueue(next); }
                }
                foreach (var room in map.Rooms)
                    Assert.That(reached[room.Y * 320 + room.X], Is.True, map.Seed + ": " + room.X + "," + room.Y);
            }
        }

        [Test]
        public void GeneratedBrushEditsCannotOverwriteFixedAssets()
        {
            using var tuner = new TerrainGenerationPreview(); var result = tuner.Generate();
            var draft = new TerrainMapDraft(); draft.OpenGenerated(result.Blueprint());
            Assert.That(draft.CanSave, Is.False);
            Assert.That(draft.Paint(100, 80, result.Material(100, 80) == 0, 2), Is.True);
            Assert.Throws<InvalidOperationException>(() => draft.Apply());
            Assert.That(draft.Cancel(), Is.True);
            CollectionAssert.AreEqual(result.CopyMaterials(), draft.CopyMaterials());
        }

        [UnityTest]
        public IEnumerator GeneratedTunerStageRendersSharedTerrain()
        {
            using var tuner = new TerrainGenerationPreview(); var map = tuner.Generate();
            using var stage = new TerrainStylePreviewStage(); using var drafts = new TerrainStyleDrafts();
            var style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>("Assets/DarkNights/Res/Terrain/StrataCave/Style.asset");
            stage.Open(tuner.Definition, map.Blueprint(), style, drafts, map.CopyMaterials(), map.CopyShapes(), map.Background);
            double deadline = EditorApplication.timeSinceStartup + 90;
            while (!stage.Ready && stage.Error == null && EditorApplication.timeSinceStartup < deadline)
            { stage.Tick(); yield return null; }
            Assert.That(stage.Error, Is.Null); Assert.That(stage.Ready, Is.True, stage.Progress);
            var target = (RenderTexture)stage.Image; var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                string folder = System.Environment.GetEnvironmentVariable("DN_TERRAIN_EVIDENCE_DIR") ??
                    Path.GetFullPath("../artifacts/cave-generation-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "tuner-formal-map.png"), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
        }

        private static void Same(PlayableTerrain expected, TerrainBlueprint actual)
        {
            Assert.That(actual.Settings.Seed, Is.EqualTo(expected.Seed));
            CollectionAssert.AreEqual(expected.CopyMaterials(), actual.CopyMaterials());
            CollectionAssert.AreEqual(expected.CopyShapes(), actual.CopyShapes());
            CollectionAssert.AreEqual(expected.CopySoftRock(), actual.CopySoftRock());
            var flags = expected.CopyProtection();
            for (int i = 0; i < flags.Length; i++) Assert.That(actual.IsProtected(i % 320, i / 320), Is.EqualTo(flags[i]));
            Assert.That(actual.Deposits.Select(d => (d.Id, d.X, d.Y, d.Capacity)),
                Is.EqualTo(expected.Deposits.Select(d => (d.Id, d.X, d.Y, d.Capacity))));
        }
    }
}
