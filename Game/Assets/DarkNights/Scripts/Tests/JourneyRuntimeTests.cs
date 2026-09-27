using DarkNights.Core.ViewData;
using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using NUnit.Framework;
using UnityEngine.TestTools;
using static DarkNights.Tests.ShipScenario;
using static DarkNights.Tests.JourneyScenario;

namespace DarkNights.Tests
{
    /// <summary>真实航程对象、可信命令和地图切换的 Editor 验收；不把同进程 Ready 断言当成实际网络传输验收。</summary>
    public sealed class JourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator OrbitBoardsPlayersAndClosedHullAllowsWalking() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            var hero = Hero(world, 0); var ship = Ship(world);
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Orbit));
            Assert.That(hero.CaptureState().Boarded, Is.True); Assert.That(ship.CaptureState().ShipPhase, Is.EqualTo(3));
            Cockpit(authority, host, world);
            Walk(authority, host, hero, ship.X + ShipGeometry.RampHinge + 8);
            // 先真实走到舱门，再持续向舱外输入，分别验证可行走距离与关闭边界。
            for (int i = 0; i < 30; i++) Input(authority, host, hero, -1);
            Assert.That(hero.CaptureState().Boarded, Is.True);
            Assert.That(hero.X - ship.X, Is.EqualTo(ShipGeometry.RampHinge + 8).Within(.01));
            Assert.That(world.Terrain.Capture().Deposits, Is.Empty);
        });

        [UnityTest]
        public IEnumerator DestinationUsesTrustedActorAndOnlyOneConcurrentSelectionWins() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); var guest = Connect(authority, 1);
            var first = Hero(world, 0); var second = Hero(world, 1);
            Assert.That(Execute(authority, host, Select(authority, world, 1, first)), Is.EqualTo(SessionResultCode.NoEffect));
            Cockpit(authority, host, world); Cockpit(authority, guest, world);
            Assert.That(Execute(authority, guest, Select(authority, world, 1, first)), Is.EqualTo(SessionResultCode.PermissionDenied));
            int lease = first.CaptureState().ControlLease;
            var request = Select(authority, world, 2, first);
            authority.Submit(host, request); authority.Submit(guest, Select(authority, world, 2, second));
            var receipts = authority.Tick();
            Assert.That(receipts.Single(r => r.PlayerSlot == 0).Code, Is.EqualTo(SessionResultCode.Applied));
            Assert.That(receipts.Single(r => r.PlayerSlot == 1).Code, Is.EqualTo(SessionResultCode.NoEffect));
            Assert.That(Ship(world).CaptureState().PilotId, Is.EqualTo(first.Id));
            Assert.That(first.CaptureState().ControlLease, Is.GreaterThan(lease));
            string id = world.Journey.Capture().JourneyId;
            Assert.That(authority.Submit(host, request).Code, Is.EqualTo(SessionResultCode.Applied));
            Assert.That(world.Journey.Capture().JourneyId, Is.EqualTo(id), "重发不能生成第二个航程。");
        });

        [UnityTest]
        public IEnumerator CancelDiscardsLateGenerationAndReleasesPilot() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); Cockpit(authority, host, world);
            var hero = Hero(world, 0); var original = world.Terrain.Map;
            Execute(authority, host, Select(authority, world, 1, hero));
            Assert.That(Execute(authority, host, Cancel(authority, world, 2, hero)), Is.EqualTo(SessionResultCode.Applied));
            for (int i = 0; i < 80; i++) { authority.Tick(); await UniTask.Yield(); }
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Orbit));
            Assert.That(world.Journey.Capture().MapId, Is.Empty); Assert.That(world.Terrain.Map, Is.SameAs(original));
            Assert.That(Ship(world).CaptureState().PilotId, Is.Zero); Assert.That(authority.Epoch, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator FailedGenerationReturnsToOrbitWithoutReplacingTerrain() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); Cockpit(authority, host, world);
            var original = world.Terrain.Map;
            Execute(authority, host, Select(authority, world, 1, Hero(world, 0))); FailGeneration(world); authority.Tick();
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Orbit));
            StringAssert.Contains("injected generation failure", world.Journey.Capture().Error);
            Assert.That(world.Terrain.Map, Is.SameAs(original)); Assert.That(Ship(world).CaptureState().PilotId, Is.Zero);
        });

        [UnityTest]
        public IEnumerator PauseHoldsCompletedGenerationUntilResume() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); Cockpit(authority, host, world);
            Execute(authority, host, Select(authority, world, 1, Hero(world, 0)));
            world.SetTime(true, 1); double elapsed = world.Journey.Capture().PhaseElapsed;
            var task = Generation(world);
            for (int i = 0; i < 1700 && !task.IsCompleted; i++) { authority.Tick(); await UniTask.Yield(); }
            Assert.That(task.IsCompleted, Is.True); authority.Tick();
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Preparing));
            Assert.That(world.Journey.Capture().PhaseElapsed, Is.EqualTo(elapsed)); Assert.That(authority.Epoch, Is.EqualTo(1));
            world.SetTime(false, 1); await Until(authority, world, JourneyPhase.ArrivalSync);
        });

        [UnityTest]
        public IEnumerator InstallChangesEpochAndWaitsForBothReadyWithoutThrust() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); var guest = Connect(authority, 1);
            Cockpit(authority, host, world); var hero = Hero(world, 0); int[] ids = world.Index.Actors.Select(a => a.Id).ToArray();
            Execute(authority, host, Select(authority, world, 1, hero)); int previousLease = hero.CaptureState().ControlLease;
            await Until(authority, world, JourneyPhase.ArrivalSync);
            Assert.That(authority.Epoch, Is.EqualTo(2)); Assert.That(authority.ReadyCount, Is.Zero);
            Assert.That(world.Terrain.Capture().WorldId, Is.EqualTo(world.Journey.Capture().MapId));
            CollectionAssert.AreEquivalent(ids, world.Index.Actors.Select(a => a.Id));
            Assert.That(world.Index.MineralDeposits.Count(), Is.GreaterThan(0));
            Assert.That(authority.AcknowledgeReady(host, 1, authority.Revision, true), Is.False);
            Assert.That(authority.AcknowledgeReady(host, 2, authority.Revision, true), Is.True);
            Assert.That(authority.SubmitInput(host, new HeroInputRequest(SessionAuthority.ProtocolVersion, 2, authority.PolicyRevision,
                hero.Id, previousLease, 1, authority.ServerTick, 1, true, false, false, false)), Is.False);
            float height = Ship(world).CaptureState().Height;
            for (int i = 0; i < 40; i++) Input(authority, host, hero, up: true);
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.ArrivalSync));
            Assert.That(Ship(world).CaptureState().Height, Is.EqualTo(height));
            int arrivalLease = hero.CaptureState().ControlLease;
            Assert.That(authority.AcknowledgeReady(guest, 2, authority.Revision, true), Is.True); authority.Tick();
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Descent));
            Assert.That(hero.CaptureState().ControlLease, Is.GreaterThan(arrivalLease)); Assert.That(hero.CaptureState().JumpHeld, Is.False);
            var codec = new ProjectionCodec(world.Catalog, world.Layout);
            Assert.That(codec.Decode(codec.Encode(authority.CaptureProjection())).World.Expedition.Journey.Phase, Is.EqualTo(JourneyPhase.Descent));
        });

        [UnityTest]
        public IEnumerator DescentCarriesPassengerAllowsTakeoverAndLandsBeforeExit() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); var guest = Connect(authority, 1);
            await Descent(authority, world, host, guest);
            var pilot = Hero(world, 0); var passenger = Hero(world, 1); var ship = Ship(world);
            float local = passenger.CaptureState().Height - ship.CaptureState().Height;
            for (int i = 0; i < 60; i++) Input(authority, host, pilot, down: true);
            Assert.That(passenger.CaptureState().Height - ship.CaptureState().Height, Is.EqualTo(local).Within(.01));
            Assert.That(Send(authority, host, 2, "pilot", pilot), Is.EqualTo(SessionResultCode.Applied));
            float hover = ship.CaptureState().Height;
            for (int i = 0; i < 40; i++) authority.Tick();
            Assert.That(ship.CaptureState().Height, Is.EqualTo(hover));
            Cockpit(authority, guest, world);
            Assert.That(Send(authority, guest, 1, "pilot", passenger), Is.EqualTo(SessionResultCode.Applied));
            Land(authority, world, guest);
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Landed)); Assert.That(world.CaptureView().Expedition.Phase, Is.EqualTo(1));
            Assert.That(Send(authority, guest, 3, "land", passenger), Is.EqualTo(SessionResultCode.NoEffect));
            Walk(authority, guest, passenger, ship.X + ShipGeometry.RampToe - 12);
            Assert.That(passenger.CaptureState().Boarded, Is.False);
            Assert.That(passenger.CaptureState().Height, Is.EqualTo(ship.CaptureState().DockHeight).Within(1));
        });

        [UnityTest]
        public IEnumerator DisconnectCancelsBeforeArrivalAndHoversAfterArrival() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            Cockpit(authority, host, world); Execute(authority, host, Select(authority, world, 1, Hero(world, 0)));
            authority.Disconnect(host, false);
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Orbit));
            host = Connect(authority, 0); await Descent(authority, world, host);
            for (int i = 0; i < 30; i++) Input(authority, host, Hero(world, 0), up: true);
            authority.Disconnect(host, false); float height = Ship(world).CaptureState().Height;
            for (int i = 0; i < 50; i++) authority.Tick();
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Descent));
            Assert.That(Ship(world).CaptureState().PilotId, Is.Zero); Assert.That(Ship(world).CaptureState().Height, Is.EqualTo(height));
        });
    }
}
