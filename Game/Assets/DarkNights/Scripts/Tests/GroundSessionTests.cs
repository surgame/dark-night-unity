using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
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
    /// <summary>
    /// 地面基础会话的真实 YYGC 回归；通过可信连接验证出生、购买、驾驶和恢复，并证明退出业务不会被旧请求触发。
    /// 测试只建立初始作者布局，操作阶段不注入角色坐标、资源或驾驶权。
    /// </summary>
    public sealed class GroundSessionTests
    {
        [UnityTest]
        public IEnumerator FourPlayersSpawnOutsideAndIdleWithoutOldGameplay() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = await Create(scope);
            using var authority = new SessionAuthority(world);
            for (int slot = 0; slot < 4; slot++)
            {
                var connection = Connect(authority, slot);
                Assert.That(authority.AcknowledgeReady(connection, authority.Epoch, authority.Revision, true), Is.True);
                var hero = Hero(world, slot).CaptureState();
                Assert.That(hero.Boarded, Is.False);
                Assert.That(hero.Height, Is.EqualTo(Ship(world).CaptureState().DockHeight).Within(2));
                Assert.That(hero.Slot0, Is.Empty);
                Assert.That(hero.JetpackEquipped, Is.False);
            }
            for (int i = 0; i < 3600; i++) authority.Tick();
            var view = world.CaptureView();
            Assert.That(view.Actors.Count, Is.EqualTo(4));
            Assert.That(view.Buildings.Count, Is.EqualTo(1));
            Assert.That(view.Worksites, Is.Empty);
            Assert.That(view.Projectiles, Is.Empty);
            Assert.That(view.Expedition.Risk, Is.Zero);
            Assert.That(view.Expedition.Clock, Is.Zero);
            Assert.That(view.Expedition.Phase, Is.Zero);
            Assert.That(view.Expedition.Journey.Phase, Is.EqualTo(JourneyPhase.Landed));
            Assert.That(view.Expedition.Ship.Phase, Is.Zero);
            Assert.That(view.Expedition.Settled, Is.False);
            Assert.That(world.SaveCodec.Parse(world.SaveCodec.Serialize(world.CaptureWorld())), Is.Not.Null);
        });

        [UnityTest]
        public IEnumerator OldCommandsAndHeldUseCannotChangeGameplay() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = await Create(scope);
            using var authority = new SessionAuthority(world);
            var host = Connect(authority, 0); var hero = Hero(world, 0);
            byte[] cells = world.Terrain.Capture().CopyMaterials();
            long sequence = 0;
            foreach (string command in new[] { "depart", "unload", "board", "recall", "launch", "emergency", "robot", "cargo", "crew", "mine", "resupply", "deploy" })
                Assert.That(Send(authority, host, ++sequence, command, hero), Is.EqualTo(SessionResultCode.InvalidRequest), command);
            foreach (var operation in new[] { SessionOperation.Recruit, SessionOperation.StartNight,
                SessionOperation.SelectDestination, SessionOperation.CancelJourney, SessionOperation.SellCarriedOre, SessionOperation.UseHeroItem })
            {
                var request = new SessionRequest(operation, SessionAuthority.ProtocolVersion, authority.Epoch,
                    authority.PolicyRevision, ++sequence, new[] { hero.Id }, targetId: Ship(world).Id,
                    kind: operation == SessionOperation.SelectDestination ? world.Flow.ActivePlanet.Id : "pickaxe",
                    controlLease: hero.CaptureState().ControlLease);
                Assert.That(JourneyScenario.Execute(authority, host, request), Is.Not.EqualTo(SessionResultCode.Applied), operation.ToString());
            }
            for (int i = 0; i < 120; i++)
            {
                var state = hero.CaptureState();
                Assert.That(authority.SubmitInput(host, new HeroInputRequest(SessionAuthority.ProtocolVersion,
                    authority.Epoch, authority.PolicyRevision, hero.Id, state.ControlLease,
                    authority.ServerTick + 1, authority.ServerTick, 0, false, true, false, false, usePressed: true)), Is.True);
                authority.Tick();
            }
            Assert.That(world.Terrain.Capture().CopyMaterials(), Is.EqualTo(cells));
            Assert.That(world.CaptureView().Projectiles, Is.Empty);
            Assert.That(world.CaptureView().Expedition.Risk, Is.Zero);
        });

        [UnityTest]
        public IEnumerator PurchaseInventoryCreditsAndUsableJetpackRestore() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = await Create(scope);
            using var authority = new SessionAuthority(world);
            var host = Connect(authority, 0); var hero = Hero(world, 0); var ship = Ship(world);
            Walk(authority, host, hero, ship.X + 32);
            Assert.That(hero.CaptureState().Boarded, Is.True);
            int balance = world.Economy.CaptureState().Credits;
            long sequence = 0;
            foreach (string item in new[] { "pickaxe", "jetpack" })
            {
                var state = hero.CaptureState();
                var request = new SessionRequest(SessionOperation.BuyEquipment, SessionAuthority.ProtocolVersion,
                    authority.Epoch, authority.PolicyRevision, ++sequence, new[] { hero.Id }, targetId: ship.Id,
                    kind: item, value: state.InventoryRevision, controlLease: state.ControlLease);
                Assert.That(JourneyScenario.Execute(authority, host, request), Is.EqualTo(SessionResultCode.Applied));
            }
            var purchased = hero.CaptureState();
            Assert.That(purchased.JetpackOwned, Is.True);
            Assert.That(purchased.JetpackEquipped, Is.True);
            Assert.That(purchased.JetpackFuel, Is.EqualTo(world.Catalog.Balance.HeroControl.FuelSeconds));
            Assert.That(world.Economy.CaptureState().Credits,
                Is.EqualTo(balance - world.Catalog.Balance.Expedition.Trade.PickaxePrice - world.Catalog.Balance.Expedition.Trade.JetpackPrice));
            string saved = world.SaveCodec.Serialize(world.CaptureWorld());
            int credits = world.Economy.CaptureState().Credits;
            world.Restore(saved);
            var restored = Hero(world, 0).CaptureState();
            Assert.That(restored.Slot0, Is.EqualTo(purchased.Slot0));
            Assert.That(restored.JetpackOwned, Is.True);
            Assert.That(restored.JetpackEquipped, Is.True);
            Assert.That(restored.JetpackFuel, Is.EqualTo(purchased.JetpackFuel));
            Assert.That(world.Economy.CaptureState().Credits, Is.EqualTo(credits));
            var invalidFuel = JObject.Parse(saved); invalidFuel["world"]["actors"][0]["jetpack_fuel"] = 900;
            Assert.Throws<FormatException>(() => world.SaveCodec.Parse(invalidFuel.ToString()));
            var npc = JObject.Parse(saved); npc["world"]["actors"][0]["manual_control"] = false;
            Assert.Throws<FormatException>(() => world.SaveCodec.Parse(npc.ToString()));
            var old = JObject.Parse(saved); old["format_version"] = 20;
            Assert.Throws<FormatException>(() => world.SaveCodec.Parse(old.ToString()));
        });

        [UnityTest]
        public IEnumerator GroundShipCanTakeOffFlySaveAndLand() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = await Create(scope);
            using var authority = new SessionAuthority(world);
            var host = Connect(authority, 0); var hero = Hero(world, 0); var ship = Ship(world);
            Walk(authority, host, hero, ship.X + ShipGeometry.PilotX);
            Assert.That(Send(authority, host, 1, "pilot", hero), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(Send(authority, host, 2, "takeoff", hero), Is.EqualTo(SessionResultCode.Applied));
            for (int i = 0; i < 600 && ship.CaptureState().ShipPhase != 3; i++) Input(authority, host, hero);
            Assert.That(ship.CaptureState().ShipPhase, Is.EqualTo(3));
            for (int i = 0; i < 45; i++) Input(authority, host, hero, up: true);
            Assert.That(ship.CaptureState().Height, Is.GreaterThan(ship.CaptureState().DockHeight + 8));
            string flyingSave = world.SaveCodec.Serialize(world.CaptureWorld());
            float flyingHeight = ship.CaptureState().Height;
            Assert.That(world.SaveCodec.Parse(flyingSave), Is.Not.Null);
            for (int i = 0; i < 300 && ship.CaptureState().Height > ship.CaptureState().DockHeight + 1; i++)
                Input(authority, host, hero, down: true);
            for (int i = 0; i < 45; i++) Input(authority, host, hero);
            Assert.That(Send(authority, host, 3, "land", hero), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(ship.CaptureState().ShipPhase, Is.Zero);
            Assert.That(Send(authority, host, 4, "pilot", hero), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(ship.CaptureState().PilotId, Is.Zero);
            world.Restore(flyingSave);
            Assert.That(Ship(world).CaptureState().ShipPhase, Is.EqualTo(3));
            Assert.That(Ship(world).CaptureState().Height, Is.EqualTo(flyingHeight).Within(.001));
            Assert.That(world.CaptureView().Expedition.Journey.Phase, Is.EqualTo(JourneyPhase.Landed));
        });

        internal static async UniTask<ObjectSession> Create(UnifiedSessionScope scope)
        {
            var config = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch")
                .SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            var planet = config.PreviewPlanet();
            var settings = config.FreezeCaveMap();
            var modifiers = config.FreezeModifiers();
            var map = await UniTask.RunOnThreadPool(() => PlanetTerrainGenerator.GenerateCandidate(planet,
                "GROUND-20261007", Guid.NewGuid().ToString("N"), settings, pipeline: modifiers));
            var definition = TerrainProfileConfig.Resolve().Definition;
            var layout = new LevelLayout(5120, RuleScenario.Layout().GroundY, 380, 770, 900, planet.DockX,
                new[] { new PlacementDefinition("ship", planet.DockX) }, Array.Empty<PlacementDefinition>(),
                Array.Empty<PlacementDefinition>(), randomTerrain: true, expedition: true);
            return scope.NewWorld(RuleScenario.Catalog(), layout, false,
                terrain: w => new SessionTerrain(w.Context, definition.LoadGameplayCatalog(), map), journeyEnabled: true);
        }
    }
}
