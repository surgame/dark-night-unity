using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using static DarkNights.Tests.JourneyScenario;
using static DarkNights.Tests.ShipScenario;

namespace DarkNights.Tests
{
    /// <summary>快速规则与真实 YYGC 命令测试；覆盖初始空装备、冲刺、购买版本和矿石出售事务。</summary>
    public sealed class ShipTradeTests
    {
        [Test]
        public void TradeValuesAndSprintBoundsAreValidated()
        {
            var rules = new ShipTradeDefinition(16, 1, 4, 10, 4, 14);
            Assert.That(rules.SaleValue(3, 2), Is.EqualTo(11));
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.SaleValue(-1, 0));
            Assert.Throws<ArgumentException>(() => new ShipTradeDefinition(0, 0, 4, 10, 4, 14));
            Assert.Throws<ArgumentException>(() => new HeroControlDefinition(1, 1, 1, 1, 1, 1, 1, 1, 4.1));
        }

        [UnityTest]
        public IEnumerator EmptyEquipmentAndSprintAreAuthoritative() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            Assert.That(new[] { f.State.Slot0, f.State.Slot1, f.State.Slot2, f.State.Slot3 }, Is.All.EqualTo(""));
            Assert.That(f.State.JetpackOwned, Is.False);
            Assert.That(f.State.JetpackFuel, Is.Zero);
            Assert.That(f.State.ExplosiveCharges, Is.Zero);
            Assert.That(f.Command(SessionOperation.ClaimHero).Code, Is.EqualTo(SessionResultCode.Applied));
            float start = f.Actor.X;
            Assert.That(f.Authority.SubmitInput(f.Host, f.Packet(horizontal: 1, sprintHeld: true)), Is.True);
            f.Step(6);
            float sprintDistance = f.Actor.X - start;
            double normalDistance = f.World.Catalog.Balance.HeroControl.WalkSpeed * .1;
            Assert.That(sprintDistance, Is.EqualTo(normalDistance * f.World.Catalog.Balance.HeroControl.SprintMultiplier).Within(.001));
            Assert.That(f.Authority.SubmitInput(f.Host, f.Packet(horizontal: 1)), Is.True);
            f.Step(6);
            Assert.That(f.Actor.X - start - sprintDistance, Is.EqualTo(normalDistance).Within(.001));
            f.Step(SessionHeroControl.InputTimeoutTicks + 2);
            Assert.That(f.State.SprintHeld, Is.False);
        });

        [UnityTest]
        public IEnumerator SaveRejectsInvalidEquipmentAndCredits() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            string clean = f.World.SaveCodec.Serialize(f.Authority.CaptureWorld());
            Assert.That(f.World.SaveCodec.Parse(clean), Is.Not.Null);
            void Reject(Action<JObject> edit)
            {
                var copy = JObject.Parse(clean);
                edit(copy);
                Assert.Throws<FormatException>(() => f.World.SaveCodec.Parse(copy.ToString()));
            }
            JObject Actor(JObject root) => root["world"]["actors"].OfType<JObject>()
                .Single(value => (int)value["id"] == f.ActorId);
            Reject(root => Actor(root)["slot_0"] = 4);
            Reject(root => { Actor(root)["slot_0"] = "f739a1d69023de54b8b90e2061afd135"; Actor(root)["slot_1"] = "f739a1d69023de54b8b90e2061afd135"; });
            Reject(root => Actor(root)["jetpack_fuel"] = 1);
            Reject(root => root["world"]["economy"]["credits"] = -1);
            Reject(root => root["world"]["economy"]["credits"] = 10000001);
        });

        [UnityTest]
        public IEnumerator ShopReservesFirstPickaxeBudget() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world);
            var host = Connect(authority, 0);
            var save = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
            save["world"]["economy"]["credits"] = 14;
            world.Restore(save.ToString());
            Assert.That(authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true), Is.True);
            var hero = Hero(world, 0); var ship = Ship(world);
            Walk(authority, host, hero, ship.X + 32);
            var pack = new SessionRequest(SessionOperation.BuyEquipment, SessionAuthority.ProtocolVersion,
                authority.Epoch, authority.PolicyRevision, 1, new[] { hero.Id }, targetId: ship.Id,
                kind: "jetpack", value: hero.CaptureState().InventoryRevision, controlLease: hero.CaptureState().ControlLease);
            Assert.That(Execute(authority, host, pack), Is.EqualTo(SessionResultCode.NoEffect));
            Assert.That(world.Economy.CaptureState().Credits, Is.EqualTo(14));
            var pickaxe = new SessionRequest(SessionOperation.BuyEquipment, SessionAuthority.ProtocolVersion,
                authority.Epoch, authority.PolicyRevision, 2, new[] { hero.Id }, targetId: ship.Id,
                kind: "pickaxe", value: hero.CaptureState().InventoryRevision, controlLease: hero.CaptureState().ControlLease);
            Assert.That(Execute(authority, host, pickaxe), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(world.Economy.CaptureState().Credits, Is.EqualTo(10));
        });

        [UnityTest]
        public IEnumerator ShopAndSaleUseLeasePositionAndAtomicState() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world);
            var host = Connect(authority, 0);
            var hero = Hero(world, 0); var ship = Ship(world);
            Assert.That(world.Economy.CaptureState().Credits, Is.EqualTo(30));
            var remote = new SessionRequest(SessionOperation.BuyEquipment, SessionAuthority.ProtocolVersion,
                authority.Epoch, authority.PolicyRevision, 1, new[] { hero.Id }, targetId: ship.Id,
                kind: "pickaxe", value: hero.CaptureState().InventoryRevision, controlLease: hero.CaptureState().ControlLease);
            Assert.That(Execute(authority, host, remote), Is.EqualTo(SessionResultCode.NoEffect));
            Walk(authority, host, hero, ship.X + 32);
            var buy = new SessionRequest(SessionOperation.BuyEquipment, SessionAuthority.ProtocolVersion,
                authority.Epoch, authority.PolicyRevision, 2, new[] { hero.Id }, targetId: ship.Id,
                kind: "pickaxe", value: hero.CaptureState().InventoryRevision, controlLease: hero.CaptureState().ControlLease);
            Assert.That(Execute(authority, host, buy), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(hero.CaptureState().Slot1, Is.EqualTo(GameCore.Objects.Definition.ObjectDefinitionDatabase.Instance.GetDefinitionByKey("item.pickaxe").Guid.ToString()));
            Assert.That(hero.CaptureState().InventoryRevision, Is.EqualTo(2));
            Assert.That(world.Economy.CaptureState().Credits, Is.EqualTo(26));
            Assert.That(authority.Submit(host, buy).Code, Is.EqualTo(SessionResultCode.Applied));
            Assert.That(world.Economy.CaptureState().Credits, Is.EqualTo(26));
            var stale = new SessionRequest(SessionOperation.BuyEquipment, SessionAuthority.ProtocolVersion,
                authority.Epoch, authority.PolicyRevision, 3, new[] { hero.Id }, targetId: ship.Id,
                kind: "pistol", value: 0, controlLease: hero.CaptureState().ControlLease);
            Assert.That(Execute(authority, host, stale), Is.EqualTo(SessionResultCode.NoEffect));
            var save = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
            var crew = save["world"]["expedition"]["Crew"].OfType<JObject>().Single(value => (int)value["Id"] == hero.Id);
            crew["Iron"] = 3; crew["Gold"] = 1;
            world.Restore(save.ToString());
            Assert.That(authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true), Is.True);
            hero = Hero(world, 0);
            ship = Ship(world);
            Walk(authority, host, hero, ship.X - 40);
            var sell = new SessionRequest(SessionOperation.SellCarriedOre, SessionAuthority.ProtocolVersion,
                authority.Epoch, authority.PolicyRevision, 4, new[] { hero.Id }, targetId: ship.Id,
                x: 1, kind: "sale", value: 3, controlLease: hero.CaptureState().ControlLease);
            Assert.That(hero.CaptureState().ControlLease, Is.GreaterThan(0));
            Assert.That(world.ValidRequest(sell), Is.True);
            Assert.That(Execute(authority, host, sell), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(hero.CaptureState().CargoIron + hero.CaptureState().CargoGold, Is.Zero);
            Assert.That(world.Economy.CaptureState().Credits, Is.EqualTo(33));
        });
    }
}
