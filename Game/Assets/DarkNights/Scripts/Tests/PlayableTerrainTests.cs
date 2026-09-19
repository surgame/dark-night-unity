using System;
using System.Collections;
using System.Collections.Generic;
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
using Newtonsoft.Json.Linq;
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
            var selected = PlayableTerrainGenerator.Generate("save-restore", Id);
            var world = scope.NewWorld(catalog, layout, false, terrain: value =>
                new SessionTerrain(value.Context, definition.LoadGameplayCatalog(), selected));
            using var authority = new SessionAuthority(world);
            var host = authority.Connect(0); authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true);
            Assert.That(world.Index.MineralDeposits.Count, Is.EqualTo(selected.Deposits.Count));
            int scattered = selected.CopyMaterials().Count(value => value >= 4 && value <= 6);
            int mineCapacity = selected.Deposits.Where(value => value.RoomKind == "mine").Sum(value => value.Capacity);
            Assert.That(mineCapacity, Is.GreaterThan(scattered), "矿室总容量必须明显高于沿途散矿格数。");
            using (var objectReplica = new ObjectReplica(scope.Resources, scope.Placements(layout), null))
            {
                objectReplica.Apply(world.CaptureView());
                Assert.That(objectReplica.Count, Is.EqualTo(world.Index.Count), "客户端必须接受动态地形矿床身份。");
            }
            var mined = world.Index.MineralDeposits.First(value => value.RoomKind == "mine");
            string minedPlacement = mined.PlacementKey;
            int minedRemaining = mined.Remaining;
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
            Assert.That(world.Index.MineralDeposits.Single(value => value.PlacementKey == minedPlacement).Remaining,
                Is.EqualTo(minedRemaining));
            var valid = world.Terrain.Map;
            Assert.Throws<FormatException>(() => world.Restore(save.Replace("\"format_version\":6", "\"format_version\":4")));
            Assert.That(world.Terrain.Map, Is.SameAs(valid));
            var state = world.Index.Actors.First(a => !a.Enemy).CaptureState();
            TerrainHeroMotion.Tick(valid, state, catalog.Balance.HeroControl, 1.0 / 60, true, false);
            Assert.That(state.Height, Is.GreaterThan(0));
            for (int i = 0; i < 300; i++) TerrainHeroMotion.Tick(valid, state, catalog.Balance.HeroControl, 1.0 / 60, false, false);
            Assert.That(state.Height, Is.EqualTo(0).Within(.001)); Assert.That(state.SupportPlatform, Is.Zero);
        });

        [UnityTest]
        public IEnumerator SoftRockMiningPersistsEmptyCellAcrossSaveRestore() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(Editor.Terrain.TerrainTestAssets.DefinitionPath);
            var catalog = RuleScenario.Catalog(); var layout = PlayableTerrainGenerator.Layout(RuleScenario.Layout());
            var selected = PlayableTerrainGenerator.Generate("soft-rock-save", Id);
            var world = scope.NewWorld(catalog, layout, false, terrain: value =>
                new SessionTerrain(value.Context, definition.LoadGameplayCatalog(), selected));
            using var authority = new SessionAuthority(world);
            var host = authority.Connect(0); authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true);

            CellCoord target = default;
            bool found = false;
            var map = world.Terrain.Map;
            for (int y = -map.Descriptor.Bounds.Height; y < 0 && !found; y++)
                for (int x = 0; x < map.Descriptor.Bounds.Width; x++)
                {
                    var position = new CellCoord(x, y);
                    if (map.IsSoftRock(position) && map.Read(position).TryGetCell(out var cell) &&
                        !cell.IsEmpty && cell.Flags == 0) { target = position; found = true; break; }
                }
            Assert.That(found, Is.True, "A destructible soft-rock fixture is required.");
            var targets = map.BuildTargets(TerrainEditAction.HandMine, target);
            var receipt = map.DestroyTrusted(1, "soft-rock-save", TerrainEditAction.HandMine, map.World,
                target, targets, _ => true, out bool applied);
            Assert.That(applied, Is.True); Assert.That(receipt.ChangedChunks.Count, Is.EqualTo(1));
            Assert.That(map.Read(target).Cell.IsEmpty, Is.True);

            string save = world.SaveCodec.Serialize(world.CaptureWorld());
            world.Restore(save);
            Assert.That(world.Terrain.Map.Read(target).Cell.IsEmpty, Is.True);
            Assert.That(world.Terrain.Map.IsSoftRock(target), Is.True);

            map = world.Terrain.Map;
            CellCoord blast = default;
            IReadOnlyList<CellCoord> blastTargets = null;
            found = false;
            for (int v = -map.Descriptor.Bounds.Height + 2; v < -2 && !found; v++)
                for (int u = 2; u < map.Descriptor.Bounds.Width - 2; u++)
                {
                    var candidate = new CellCoord(u, v);
                    var right = new CellCoord(u + 1, v);
                    var above = new CellCoord(u, v + 1);
                    var aboveRight = new CellCoord(u + 1, v + 1);
                    if (!map.Read(candidate).TryGetCell(out var centerCell) || centerCell.IsEmpty ||
                        (centerCell.Flags & 1) != 0 || selected.Material(u, -v) == 8 ||
                        !map.Read(right).TryGetCell(out var rightCell) || rightCell.IsEmpty ||
                        (rightCell.Flags & 1) != 0 || selected.Material(u + 1, -v) == 8)
                        continue;
                    var candidateTargets = map.BuildTargets(TerrainEditAction.Explosive, candidate);
                    if (candidateTargets.Contains(candidate) && candidateTargets.Contains(right) && map.Read(above).Cell.IsEmpty &&
                        map.Read(aboveRight).Cell.IsEmpty)
                    { blast = candidate; blastTargets = candidateTargets; found = true; break; }
                }
            Assert.That(found, Is.True, "A blastable two-cell support edge is required.");

            var document = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
            var actor = ((JArray)document["world"]["actors"]).OfType<JObject>()
                .First(value => !(bool)value["enemy"]);
            actor["x"] = (blast.U + .5f) * PlayableTerrain.CellPixels;
            actor["height"] = PlayableTerrain.OriginY - (-blast.V - .5f) * PlayableTerrain.CellPixels;
            actor["vertical_speed"] = 0;
            actor["support_platform"] = 0;
            world.Restore(document.ToString(Newtonsoft.Json.Formatting.None));

            map = world.Terrain.Map;
            var state = world.Index.Actors.First(value => !value.Enemy).CaptureState();
            TerrainHeroMotion.Tick(map, state, catalog.Balance.HeroControl, 1.0 / 60, false, false);
            float supportedHeight = state.Height;
            Assert.That(state.SupportPlatform, Is.Zero);
            map.DestroyTrusted(1, "collision-blast", TerrainEditAction.Explosive, map.World,
                blast, blastTargets, _ => true, out applied);
            Assert.That(applied, Is.True);
            TerrainHeroMotion.Tick(map, state, catalog.Balance.HeroControl, 1.0 / 60, false, false);
            Assert.That(state.SupportPlatform, Is.EqualTo(-1));
            Assert.That(state.Height, Is.LessThan(supportedHeight), "Authoritative collision must observe the blasted cells immediately.");
        });
    }
}
