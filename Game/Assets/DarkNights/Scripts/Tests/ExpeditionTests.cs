using System;
using System.Collections;
using System.Linq;
using AnyRules.Next.Authoring;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>远征快速验收：真实 YYGC 对象、可信请求、货物守恒、跨局结算、投影与坡形保存；不代替跨进程与人工试玩。</summary>
    public sealed class ExpeditionTests
    {
        private const string WorldId = "9765fd14785b4b0bb4ab7d7b7286b384";
        private static ObjectSession Create(UnifiedSessionScope scope)
        {
            var catalog = RuleScenario.Catalog();
            var layout = new LevelLayout(5120, RuleScenario.Layout().GroundY, 380, 770, 900, 568,
                new[] { new PlacementDefinition("ship", 568) }, Array.Empty<PlacementDefinition>(), Array.Empty<PlacementDefinition>(),
                randomTerrain: true, expedition: true);
            var map = ExpeditionTerrainGenerator.Generate("EXPEDITION-QUICK-01", WorldId);
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(Editor.Terrain.CaveTerrainAssets.DefinitionPath);
            return scope.NewWorld(catalog, layout, false, terrain: w => new SessionTerrain(w.Context, definition.LoadGameplayCatalog(), map));
        }
        private static SessionConnection Connect(SessionAuthority authority, int slot)
        {
            var c = authority.Connect(slot); Assert.That(authority.AcknowledgeReady(c, authority.Epoch, authority.Revision, true), Is.True); return c;
        }
        private static SessionRequest Request(SessionAuthority authority, long sequence, string kind, ActorBehaviour hero = null, int target = 0) =>
            new SessionRequest(SessionOperation.Expedition, SessionAuthority.ProtocolVersion, authority.Epoch, authority.PolicyRevision,
                sequence, hero == null ? null : new[] { hero.Id }, targetId: target, kind: kind, controlLease: hero?.CaptureState().ControlLease ?? 0);
        private static SessionResultCode Send(SessionAuthority authority, SessionConnection c, SessionRequest request)
        { authority.Submit(c, request); return authority.Tick().Single(r => r.Sequence == request.Sequence).Code; }

        [Test]
        public void HundredCompactSeedsHaveIndependentMineralsAndValidSlopes()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var map = ExpeditionTerrainGenerator.Generate("EXP-" + seed, WorldId);
                Assert.That(map.CopyMaterials().Any(v => v is 4 or 5 or 6), Is.False);
                Assert.That(map.Rooms.All(r => r.Width <= 36 && r.Height <= 24), Is.True);
                Assert.That(map.Deposits.Count, Is.EqualTo(12));
                CollectionAssert.AreEqual(map.CopyShapes(), map.Blueprint().CopyShapes());
                CollectionAssert.AreEqual(map.CopyMaterials(), ExpeditionTerrainGenerator.Generate("EXP-" + seed, WorldId).CopyMaterials());
            }
        }
        [UnityTest]
        public IEnumerator FormalMapSaveAndNetworkProjectionKeepShapes() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); Connect(authority, 0);
            string json = world.SaveCodec.Serialize(world.CaptureWorld());
            byte[] shapes = world.Terrain.Capture().CopyShapes(); Assert.That(shapes.Any(s => s != 0), Is.True);
            world.Restore(json); CollectionAssert.AreEqual(shapes, world.Terrain.Capture().CopyShapes());
            var codec = new ProjectionCodec(world.Catalog, world.Layout);
            var frame = authority.CaptureProjection();
            var restored = codec.Decode(codec.Encode(frame));
            Assert.That(restored.World.Expedition.Run, Is.EqualTo(1));
            Assert.That(restored.World.Expedition.Crew.Count, Is.EqualTo(world.Index.Actors.Count()));
        });
        [UnityTest]
        public IEnumerator CargoSettlesOnceAndUpgradeChangesSecondDeparture() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            Assert.That(Send(authority, host, Request(authority, 1, "depart")), Is.EqualTo(SessionResultCode.Applied));
            // 已采矿货袋夹具；此用例只验证卸货/结算，手采另测，不把注入算真实探索。
            var save = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
            save["world"]["expedition"]["Crew"][0]["Iron"] = 12;
            world.Restore(save.ToString()); host = Connect(authority, 0);
            var hero = world.Index.Actors.Single(a => a.CaptureState().OwnerSlot == 0);
            Assert.That(Send(authority, host, Request(authority, 2, "unload", hero)), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(world.CaptureView().Expedition.Devices.Sum(d => d.Iron), Is.EqualTo(12));
            Assert.That(world.Economy.Stock.Iron, Is.Zero);
            Assert.That(Send(authority, host, Request(authority, 3, "board", hero)), Is.EqualTo(SessionResultCode.Applied));
            var launch = Request(authority, 4, "emergency"); Send(authority, host, launch);
            authority.Submit(host, launch); authority.Tick();
            for (int i = 0; i < 490; i++) authority.Tick();
            Assert.That(world.Economy.Stock.Iron, Is.EqualTo(12));
            string settled = world.SaveCodec.Serialize(world.CaptureWorld()); world.Restore(settled); host = Connect(authority, 0);
            for (int i = 0; i < 60; i++) authority.Tick();
            Assert.That(world.Economy.Stock.Iron, Is.EqualTo(12));
            Assert.That(Send(authority, host, Request(authority, 5, "robot")), Is.EqualTo(SessionResultCode.Applied));
            Send(authority, host, Request(authority, 6, "depart"));
            Assert.That(world.CaptureView().Expedition.Run, Is.EqualTo(2));
            Assert.That(world.Index.Actors.Count(a => a.RuleKey == "hauler"), Is.EqualTo(1));
            Assert.That(world.Index.Buildings.Count(), Is.EqualTo(5));
            Assert.That(world.Economy.Stock.Iron, Is.EqualTo(2));
            for (int i = 0; i < 1800; i++) authority.Tick();
            Assert.That(world.CaptureView().Expedition.Devices.Count(d => d.Stage == 3), Is.EqualTo(5), "四设备应真实搬运展开");
            string mid = world.SaveCodec.Serialize(world.CaptureWorld()); world.Restore(mid);
            Assert.That(world.CaptureView().Expedition.Devices.Count(d => d.Stage == 3), Is.EqualTo(5));
            host = Connect(authority, 0); hero = world.Index.Actors.Single(a => a.CaptureState().OwnerSlot == 0);
            Send(authority, host, Request(authority, 7, "recall"));
            Send(authority, host, Request(authority, 8, "board", hero));
            for (int i = 0; i < 4200 && world.CaptureView().Expedition.Crew.Any(a => !a.Boarded); i++) authority.Tick();
            Assert.That(world.CaptureView().Expedition.Devices.Count(d => d.Stage == 6), Is.EqualTo(4), "四设备均应被实际撤收");
            Assert.That(Send(authority, host, Request(authority, 9, "launch")), Is.EqualTo(SessionResultCode.Applied));
        });
        [UnityTest]
        public IEnumerator GuestCannotLaunchAndReconnectReclaimsCargoOwner() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); var guest = Connect(authority, 1);
            Send(authority, host, Request(authority, 1, "depart"));
            Assert.That(Send(authority, guest, Request(authority, 1, "emergency")), Is.EqualTo(SessionResultCode.PermissionDenied));
            int id = world.Index.Actors.Single(a => a.CaptureState().OwnerSlot == 1).Id;
            authority.Disconnect(guest); guest = Connect(authority, 1);
            Assert.That(world.Index.Actors.Single(a => a.CaptureState().OwnerSlot == 1).Id, Is.EqualTo(id));
            Assert.That(world.Index.Actors.Count(), Is.EqualTo(2));
        });
        [UnityTest]
        public IEnumerator StarterRouteUsesFiniteFuelAndMiningKeepsOreIndependent() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            Send(authority, host, Request(authority, 1, "depart"));
            var hero = world.Index.Actors.Single(a => a.CaptureState().OwnerSlot == 0);
            var deposit = world.Index.MineralDeposits.Cast<MineralDepositBehaviour>().First();
            int inputSequence = 0;
            for (int tick = 0; tick < 1300 && hero.X < deposit.X - 5; tick++)
            {
                var s = hero.CaptureState();
                authority.SubmitInput(host, new HeroInputRequest(SessionAuthority.ProtocolVersion, authority.Epoch, authority.PolicyRevision,
                    hero.Id, s.ControlLease, ++inputSequence, authority.ServerTick, 1, false, false, false, false));
                authority.Tick();
            }
            Assert.That(hero.X, Is.GreaterThan(deposit.X - 10), "必经矿房必须能实际走到");
            var actor = hero.CaptureState();
            authority.SubmitInput(host, new HeroInputRequest(SessionAuthority.ProtocolVersion, authority.Epoch, authority.PolicyRevision,
                hero.Id, actor.ControlLease, ++inputSequence, authority.ServerTick, 0, false, false, false, false));
            var select = new SessionRequest(SessionOperation.SelectHeroItem, SessionAuthority.ProtocolVersion, authority.Epoch,
                authority.PolicyRevision, 2, new[] { hero.Id }, value: 1, controlLease: actor.ControlLease);
            Send(authority, host, select);
            var use = new SessionRequest(SessionOperation.UseHeroItem, SessionAuthority.ProtocolVersion, authority.Epoch,
                authority.PolicyRevision, 3, new[] { hero.Id }, targetId: deposit.Id, kind: "pickaxe",
                value: hero.CaptureState().SelectionRevision, controlLease: actor.ControlLease);
            byte[] before = world.Terrain.Capture().CopyMaterials();
            Assert.That(Send(authority, host, use), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(deposit.Remaining, Is.EqualTo(79)); Assert.That(hero.CaptureState().CargoIron, Is.EqualTo(1));
            CollectionAssert.AreEqual(before, world.Terrain.Capture().CopyMaterials());
            for (int tick = 0; tick < 1500 && hero.X > 650; tick++)
            {
                var s = hero.CaptureState();
                authority.SubmitInput(host, new HeroInputRequest(SessionAuthority.ProtocolVersion, authority.Epoch, authority.PolicyRevision,
                    hero.Id, s.ControlLease, ++inputSequence, authority.ServerTick, -1, false, false, tick % 45 == 0, false));
                authority.Tick();
            }
            Assert.That(hero.X, Is.LessThan(655), "矿房需能有限燃料返回船边");
        });
        [UnityTest]
        public IEnumerator MinerCanReachStarterDepositAndDeliverCargo() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            var save = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
            save["world"]["expedition"]["CrewModule"] = 1; save["world"]["expedition"]["RobotModule"] = 1;
            world.Restore(save.ToString()); host = Connect(authority, 0);
            Send(authority, host, Request(authority, 1, "depart"));
            var hero = world.Index.Actors.Single(a => a.CaptureState().OwnerSlot == 0);
            var deposit = world.Index.MineralDeposits.Cast<MineralDepositBehaviour>().First();
            Assert.That(Send(authority, host, Request(authority, 2, "mine", hero, deposit.Id)), Is.EqualTo(SessionResultCode.Applied));
            for (int tick = 0; tick < 4600 && world.CaptureView().Expedition.Devices.Sum(d => d.Iron) == 0; tick++) authority.Tick();
            var frame = world.CaptureView().Expedition;
            Assert.That(deposit.Remaining, Is.LessThan(80));
            Assert.That(frame.Devices.Sum(d => d.Iron) + frame.Crew.Sum(a => a.Iron) + frame.LostCargo, Is.EqualTo(80 - deposit.Remaining));
            Assert.That(frame.Devices.Sum(d => d.Iron), Is.GreaterThan(0), "矿工应实际交货");
        });
        [UnityTest]
        public IEnumerator SettlementWriteFailureRollsBackAndRetryPersistsOnce() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            Send(authority, host, Request(authority, 1, "depart"));
            var save = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
            save["world"]["expedition"]["Devices"][0]["Iron"] = 12;
            world.Restore(save.ToString()); host = Connect(authority, 0);
            world.Expedition.CommitSave = _ => throw new System.IO.IOException("injected full disk");
            Send(authority, host, Request(authority, 2, "emergency"));
            for (int tick = 0; tick < 490; tick++) authority.Tick();
            Assert.That(world.Paused, Is.True); Assert.That(world.Economy.Stock.Iron, Is.Zero);
            Assert.That(world.CaptureView().Expedition.Settled, Is.False);
            Assert.That(world.CaptureView().Expedition.Devices.Sum(d => d.Iron), Is.EqualTo(12));
            string saved = null; int writes = 0;
            world.Expedition.CommitSave = snapshot => { saved = world.SaveCodec.Serialize(snapshot); writes++; };
            world.SetTime(false, 1);
            for (int tick = 0; tick < 60; tick++) authority.Tick();
            Assert.That(writes, Is.EqualTo(1)); Assert.That(world.Economy.Stock.Iron, Is.EqualTo(12));
            world.Restore(saved);
            for (int tick = 0; tick < 60; tick++) authority.Tick();
            Assert.That(writes, Is.EqualTo(1)); Assert.That(world.Economy.Stock.Iron, Is.EqualTo(12));
        });
        [UnityTest]
        public IEnumerator NegativeCargoSaveIsRejectedWithoutRetiringWorld() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); Connect(authority, 0);
            var original = world.Terrain.Map;
            var save = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld())); save["world"]["expedition"]["Crew"][0]["Iron"] = -1;
            Assert.Throws<FormatException>(() => world.Restore(save.ToString())); Assert.That(world.Terrain.Map, Is.SameAs(original));
        });
    }
}
