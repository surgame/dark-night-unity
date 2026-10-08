using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Definition;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using static DarkNights.Tests.ShipScenario;

namespace DarkNights.Tests
{
    /// <summary>正式地面会话的装备输入回归；购买、移动和使用走可信入口，已持有炸药及个人货物通过严格存档安排夹具。</summary>
    public sealed class GroundEquipmentTests
    {
        [UnityTest]
        public IEnumerator PurchasedPistolFiresOnceAndProjectileRestoresAndExpires() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = await GroundSessionTests.Create(scope);
            using var authority = new SessionAuthority(world);
            var host = Connect(authority, 0); var hero = Hero(world, 0);
            Buy(world, authority, host, hero, "pistol");
            Walk(authority, host, hero, Ship(world).X + ShipGeometry.RampToe - 64);
            long before = world.Projectiles.CaptureState().NextViewId;
            var input = Packet(authority, hero, aim: 180, pressed: true, released: true);
            Assert.That(authority.SubmitInput(host, input), Is.True);
            Assert.That(authority.SubmitInput(host, input), Is.False);
            authority.Tick();
            Assert.That(world.Projectiles.CaptureState().NextViewId, Is.EqualTo(before + 1));
            Assert.That(world.CaptureView().Projectiles.Single().Kind, Is.EqualTo(1));
            string saved = world.SaveCodec.Serialize(world.CaptureWorld());
            world.Restore(saved);
            Assert.That(world.CaptureView().Projectiles.Single().Kind, Is.EqualTo(1));
            for (int i = 0; i < 120; i++) authority.Tick();
            Assert.That(world.CaptureView().Projectiles, Is.Empty);
            AssertQuiet(world);
        });

        [UnityTest]
        public IEnumerator PickaxeDamagesGroundAndPersonalCargoRestoresWithoutRisk() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = await GroundSessionTests.Create(scope);
            using var authority = new SessionAuthority(world);
            var host = Connect(authority, 0); var hero = Hero(world, 0);
            Buy(world, authority, host, hero, "pickaxe");
            Walk(authority, host, hero, Ship(world).X + ShipGeometry.RampToe - 64);
            var map = world.Terrain.Map;
            var state = hero.CaptureState();
            var tool = world.Resources.Equipment.Mining(state.Slot1);
            Assert.That(TerrainMiningQuery.FirstSurface(map, state.X, state.Height + tool.HandHeight,
                0, -1, tool.Reach, out var cell, out _), Is.True);
            var value = map.Read(cell).Cell;
            int before = map.Query(cell).State.Durability;
            var target = new HeroMiningTarget(map.World.WorldId.ToString().Replace("-", ""), map.World.Epoch,
                cell.U, cell.V, value.TileId, value.Flags, HeroMiningTargetKind.Foreground, 0, map.ContentVersion(cell), 0);
            Assert.That(authority.SubmitInput(host, Packet(authority, hero, aim: -90, pressed: true, mining: target)), Is.True);
            for (int i = 0; i < 22; i++) authority.Tick();
            Assert.That(map.Read(cell).Cell.IsEmpty || map.Query(cell).State.Durability < before, Is.True);
            Assert.That(world.CaptureView().Expedition.Risk, Is.Zero);
            var saved = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
            var cargo = saved["world"]["expedition"]["Crew"].OfType<JObject>().Single(c => (int)c["Id"] == hero.Id);
            cargo["Iron"] = 3; cargo["Gold"] = 1;
            world.Restore(saved.ToString());
            var restored = Hero(world, 0).CaptureState();
            Assert.That(restored.CargoIron, Is.EqualTo(3)); Assert.That(restored.CargoGold, Is.EqualTo(1));
            AssertQuiet(world);
        });

        [UnityTest]
        public IEnumerator PurchasedJetpackConsumesFuelAndAirborneStateRestores() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = await GroundSessionTests.Create(scope);
            using var authority = new SessionAuthority(world);
            var host = Connect(authority, 0); var hero = Hero(world, 0);
            Buy(world, authority, host, hero, "jetpack");
            Walk(authority, host, hero, Ship(world).X + ShipGeometry.RampToe - 64);
            float ground = hero.CaptureState().Height;
            for (int i = 0; i < 90; i++)
            {
                Assert.That(authority.SubmitInput(host, Packet(authority, hero, jump: true, jumpPressed: i == 0)), Is.True);
                authority.Tick();
            }
            var flying = hero.CaptureState();
            Assert.That(flying.JetpackEquipped, Is.True);
            Assert.That(flying.JetpackFuel, Is.LessThan(world.Catalog.Balance.HeroControl.FuelSeconds));
            Assert.That(flying.Height, Is.GreaterThan(ground + 8));
            world.Restore(world.SaveCodec.Serialize(world.CaptureWorld()));
            var restored = Hero(world, 0).CaptureState();
            Assert.That(restored.Height, Is.EqualTo(flying.Height));
            Assert.That(restored.JetpackFuel, Is.EqualTo(flying.JetpackFuel));
            Assert.That(restored.JetpackEquipped, Is.True);
            AssertQuiet(world);
        });

        [UnityTest]
        public IEnumerator SavedBombCanThrowRestoreExplodeAndRemainBounded() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = await GroundSessionTests.Create(scope);
            using var authority = new SessionAuthority(world);
            var host = Connect(authority, 0); var hero = Hero(world, 0);
            var saved = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
            var actor = saved["world"]["actors"].OfType<JObject>().Single(a => (int)a["id"] == hero.Id);
            actor["slot_1"] = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("item.bomb").Guid.ToString();
            actor["selected_item"] = 1;
            actor["inventory_revision"] = 1; actor["explosive_charges"] = 3;
            world.Restore(saved.ToString());
            Assert.That(authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true), Is.True);
            hero = Hero(world, 0);
            Assert.That(authority.SubmitInput(host, Packet(authority, hero, aim: -90, pressed: true, released: true)), Is.True);
            authority.Tick();
            Assert.That(hero.CaptureState().ExplosiveCharges, Is.EqualTo(2));
            Assert.That(world.CaptureView().Projectiles.Single().Kind, Is.EqualTo(2));
            world.Restore(world.SaveCodec.Serialize(world.CaptureWorld()));
            Assert.That(world.CaptureView().Projectiles.Single().Kind, Is.EqualTo(2));
            for (int i = 0; i < 220; i++) authority.Tick();
            Assert.That(world.Projectiles.CaptureState().Ballistics.Length, Is.EqualTo(HandheldConfig.PoolCapacity));
            Assert.That(world.CaptureView().Projectiles, Is.Empty);
            AssertQuiet(world);
        });

        private static void Buy(ObjectSession world, SessionAuthority authority, SessionConnection host, ActorBehaviour hero, string item)
        {
            Walk(authority, host, hero, Ship(world).X + 32);
            var state = hero.CaptureState();
            var request = new SessionRequest(SessionOperation.BuyEquipment, SessionAuthority.ProtocolVersion,
                authority.Epoch, authority.PolicyRevision, authority.ServerTick + 1000, new[] { hero.Id },
                targetId: Ship(world).Id, kind: item, value: state.InventoryRevision, controlLease: state.ControlLease);
            Assert.That(JourneyScenario.Execute(authority, host, request), Is.EqualTo(SessionResultCode.Applied));
        }

        private static HeroInputRequest Packet(SessionAuthority authority, ActorBehaviour hero, float aim = 0,
            bool pressed = false, bool released = false, bool jump = false, bool jumpPressed = false, HeroMiningTarget mining = default)
        {
            var state = hero.CaptureState();
            return new HeroInputRequest(SessionAuthority.ProtocolVersion, authority.Epoch, authority.PolicyRevision,
                hero.Id, state.ControlLease, authority.ServerTick + 1, authority.ServerTick, 0, jump, false,
                jumpPressed, false, aim, state.SelectionRevision, pressed, released, false, false, mining);
        }

        private static void AssertQuiet(ObjectSession world)
        {
            var view = world.CaptureView();
            Assert.That(view.Actors.Count, Is.EqualTo(1)); Assert.That(view.Buildings.Count, Is.EqualTo(1));
            Assert.That(view.Expedition.Phase, Is.Zero); Assert.That(view.Expedition.Risk, Is.Zero);
            Assert.That(view.Expedition.Clock, Is.Zero); Assert.That(view.Expedition.Settled, Is.False);
        }
    }
}
