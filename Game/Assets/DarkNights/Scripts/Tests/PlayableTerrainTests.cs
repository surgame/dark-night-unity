using System;
using System.Collections;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Networking;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using DarkNights.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>随机开局的确定性、全图同步容量、碰撞和原子保存恢复检查；使用实际 YYGC 对象，不能替代独立进程验收。</summary>
    public sealed class PlayableTerrainTests
    {
        private const string Id = "ac07eb5be9c646bf911002ed4c2bceef";
        [Test]
        public void SelectionIsDeterministicAndKeepsCampClear()
        {
            var a = PlayableTerrainGenerator.Generate("selection-a", Id);
            var b = PlayableTerrainGenerator.Generate("selection-a", Id);
            var c = PlayableTerrainGenerator.Generate("selection-b", Id);
            CollectionAssert.AreEqual(a.CopyMaterials(), b.CopyMaterials());
            Assert.That(a.CopyMaterials().SequenceEqual(c.CopyMaterials()), Is.False);
            for (int x = 0; x < PlayableTerrain.CampColumns; x++)
            {
                Assert.That(a.Material(x, 39), Is.Zero); Assert.That(a.Material(x, 40), Is.EqualTo(1));
            }
            var copy = a.CopyMaterials(); copy[0] = 8; Assert.That(a.Material(0, 0), Is.Zero);
        }
        [UnityTest]
        public IEnumerator FullMapStreamAndSaveRestoreShareFinalCells() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(Editor.Terrain.TerrainTestAssets.DefinitionPath);
            var catalog = RuleScenario.Catalog(); var layout = PlayableTerrainGenerator.Layout(RuleScenario.Layout());
            var world = scope.NewWorld(catalog, layout, false);
            var selected = PlayableTerrainGenerator.Generate("save-restore", Id);
            world.Terrain = new SessionTerrain(world.Context, definition.LoadGameplayCatalog(), selected);
            using var authority = new SessionAuthority(world);
            var host = authority.Connect(0); authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true);
            var map = world.Terrain.Map; var gameplay = definition.LoadGameplayCatalog();
            string visual = new string('a', 64);
            var stream = TerrainMapNetworking.OpenStream(map, TerrainMapNetworking.Handshake(map, gameplay, visual), 1, _ => true, () => 1);
            var replica = TerrainMapNetworking.CreateReplica(gameplay, visual);
            while (stream.QueuedPackets > 0) replica.Receive(stream.Dequeue());
            stream.Subscribe(map.Descriptor.Bounds);
            while (stream.QueuedPackets > 0) replica.Receive(stream.Dequeue());
            Assert.That(replica.CommitId, Is.GreaterThan(0)); Assert.That(replica.PendingCount, Is.Zero);
            int[] miniMap = CampMap.CaptureTerrainProfile(replica);
            Assert.That(miniMap.Take(PlayableTerrain.CampColumns).All(row => row == PlayableTerrain.CampRow), Is.True);
            Assert.That(miniMap.Skip(PlayableTerrain.CampColumns).Distinct().Count(), Is.GreaterThan(3));
            for (int y = 0; y < 192; y++) for (int x = 0; x < 320; x++)
                Assert.That(replica.Read(new CellCoord(x, -y)).Cell, Is.EqualTo(map.Read(new CellCoord(x, -y)).Cell));
            long scans = stream.PublicationScanCount;
            for (int i = 0; i < 120; i++) stream.Publish();
            Assert.That(stream.PublicationScanCount, Is.EqualTo(scans));
            string save = world.SaveCodec.Serialize(world.CaptureWorld());
            var previous = map.World;
            world.Restore(save);
            Assert.That(world.Terrain.Map.World.Equals(previous), Is.False);
            CollectionAssert.AreEqual(selected.CopyMaterials(), world.Terrain.Capture().CopyMaterials());
            CollectionAssert.AreEqual(selected.CopyProtection(), world.Terrain.Capture().CopyProtection());
            CollectionAssert.AreEqual(selected.CopySoftRock(), world.Terrain.Capture().CopySoftRock());
            Assert.That(world.Terrain.Capture().Deposits.Count, Is.EqualTo(selected.Deposits.Count));
            Assert.That(world.Terrain.Capture().Rooms.Count, Is.EqualTo(selected.Rooms.Count));
            var valid = world.Terrain.Map;
            Assert.Throws<FormatException>(() => world.Restore(save.Replace("\"format_version\":5", "\"format_version\":4")));
            Assert.That(world.Terrain.Map, Is.SameAs(valid));
            var state = world.Index.Actors.First(a => !a.Enemy).CaptureState();
            TerrainHeroMotion.Tick(valid, state, catalog.Balance.HeroControl, 1.0 / 60, true, false);
            Assert.That(state.Height, Is.GreaterThan(0));
            for (int i = 0; i < 300; i++) TerrainHeroMotion.Tick(valid, state, catalog.Balance.HeroControl, 1.0 / 60, false, false);
            Assert.That(state.Height, Is.EqualTo(0).Within(.001)); Assert.That(state.SupportPlatform, Is.Zero);
        });
    }
}
