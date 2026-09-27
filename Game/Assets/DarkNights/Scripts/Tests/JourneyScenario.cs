using DarkNights.Core.ViewData;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AnyRules.Next.Authoring;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using NUnit.Framework;
using UnityEditor;
using static DarkNights.Tests.ShipScenario;

namespace DarkNights.Tests
{
    /// <summary>航程验收的真实对象夹具；从空太空地图开始，走可信输入选址和实际后台生成，不注入地图、坐标或驾驶权。</summary>
    internal static class JourneyScenario
    {
        internal static ObjectSession Create(UnifiedSessionScope scope, Action<ExpeditionFlowConfig> configure = null)
        {
            var layout = new LevelLayout(5120, RuleScenario.Layout().GroundY, 380, 770, 900, 568,
                new[] { new PlacementDefinition("ship", 568) }, Array.Empty<PlacementDefinition>(), Array.Empty<PlacementDefinition>(),
                randomTerrain: true, expedition: true);
            var map = PlanetTerrainGenerator.Space("6765fd14785b4b0bb4ab7d7b7286b384");
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(Editor.Terrain.CaveTerrainAssets.DefinitionPath);
            return scope.NewWorld(RuleScenario.Catalog(), layout, false,
                terrain: w => new SessionTerrain(w.Context, definition.LoadGameplayCatalog(), map), journeyEnabled: true,
                configureJourney: configure);
        }

        internal static SessionRequest Select(SessionAuthority authority, ObjectSession world, long sequence, ActorBehaviour hero,
            int? revision = null, int? lease = null, string planet = null) => new SessionRequest(SessionOperation.SelectDestination,
                SessionAuthority.ProtocolVersion, authority.Epoch, authority.PolicyRevision, sequence, new[] { hero.Id },
                targetId: Ship(world).Id, kind: planet ?? world.Flow.Planets.First(p => p.Enabled).Id,
                value: revision ?? world.Journey.Capture().Revision, controlLease: lease ?? hero.CaptureState().ControlLease);

        internal static SessionRequest Cancel(SessionAuthority authority, ObjectSession world, long sequence, ActorBehaviour hero) =>
            new SessionRequest(SessionOperation.CancelJourney, SessionAuthority.ProtocolVersion, authority.Epoch, authority.PolicyRevision,
                sequence, new[] { hero.Id }, targetId: Ship(world).Id, value: world.Journey.Capture().Revision,
                controlLease: hero.CaptureState().ControlLease);

        internal static SessionResultCode Execute(SessionAuthority authority, SessionConnection connection, SessionRequest request)
        {
            var receipt = authority.Submit(connection, request);
            if (receipt.Code != SessionResultCode.Pending) return receipt.Code;
            return authority.Tick().Single(r => r.PlayerSlot == connection.PlayerSlot && r.Sequence == request.Sequence).Code;
        }

        internal static void Cockpit(SessionAuthority authority, SessionConnection connection, ObjectSession world) =>
            Walk(authority, connection, Hero(world, connection.PlayerSlot), Ship(world).X + ShipGeometry.PilotX);

        internal static async UniTask Until(SessionAuthority authority, ObjectSession world, JourneyPhase phase)
        {
            for (int i = 0; i < 1700 && world.Flow.Phase != phase; i++)
            {
                authority.Tick();
                if (world.Flow.Phase == JourneyPhase.Orbit)
                    Assert.Fail("航程回退：" + world.Journey.Capture().Error);
                await UniTask.Yield();
            }
            Assert.That(world.Flow.Phase, Is.EqualTo(phase), world.Journey.Capture().Error);
        }

        internal static async UniTask Descent(SessionAuthority authority, ObjectSession world, params SessionConnection[] connections)
        {
            Cockpit(authority, connections[0], world);
            Assert.That(Execute(authority, connections[0], Select(authority, world, 1, Hero(world, connections[0].PlayerSlot))), Is.EqualTo(SessionResultCode.Applied));
            await Until(authority, world, JourneyPhase.ArrivalSync);
            foreach (var connection in connections)
                Assert.That(authority.AcknowledgeReady(connection, authority.Epoch, authority.Revision, true), Is.True);
            authority.Tick();
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Descent));
        }

        internal static Task<PlayableTerrain> Generation(ObjectSession world) =>
            (Task<PlayableTerrain>)typeof(ExpeditionFlowBehaviour).GetField("generation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world.Flow);

        internal static void Land(SessionAuthority authority, ObjectSession world, SessionConnection pilot, bool waitForDoor = true)
        {
            var hero = Hero(world, pilot.PlayerSlot); var ship = Ship(world);
            for (int i = 0; i < 1800 && ship.CaptureState().Height > ship.CaptureState().DockHeight + 36; i++)
                Input(authority, pilot, hero, down: true);
            for (int i = 0; i < 1200 && world.Flow.Phase != JourneyPhase.Landed; i++) Input(authority, pilot, hero);
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Landed));
            Assert.That(ship.CaptureState().PilotId, Is.Zero);
            if (!waitForDoor) return;
            int doorTicks = (int)Math.Ceiling(world.Catalog.Balance.Expedition.Ship.DoorSeconds * 60) + 2;
            for (int i = 0; i < doorTicks && ship.CaptureState().ShipDoorClock > 0; i++) authority.Tick();
            Assert.That(ship.CaptureState().ShipDoorClock, Is.Zero);
        }

        internal static void FailGeneration(ObjectSession world)
        {
            // 只替换后台任务结果，验证正式失败回退；旧任务仍由原取消令牌收尾并观察异常。
            var previous = Generation(world);
            _ = previous?.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            typeof(ExpeditionFlowBehaviour).GetField("generation", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(world.Flow, Task.FromException<PlayableTerrain>(new InvalidOperationException("injected generation failure")));
        }
    }
}
