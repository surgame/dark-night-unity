using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static DarkNights.Tests.ShipScenario;
using static DarkNights.Tests.JourneyScenario;

namespace DarkNights.Tests
{
    /// <summary>多目的地竞争、权限切换和真实地图提交异常验收；所有内容改动仅落在测试克隆的会话定义。</summary>
    public sealed class JourneyAdversarialTests
    {
        [UnityTest]
        public IEnumerator FourPlayersCompetingForDifferentPlanetsCommitExactlyOneDestination() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = JourneyScenario.Create(scope, c =>
            {
                c.Planets = new List<PlanetPreset>();
                for (int i = 0; i < 4; i++) c.Planets.Add(new PlanetPreset
                { Id = "competition-" + i, DisplayName = "竞争星球 " + i, DockColumn = 36 + i * 8 });
            });
            using var authority = new SessionAuthority(world);
            var players = Enumerable.Range(0, 4).Select(slot => Connect(authority, slot)).ToArray();
            foreach (var player in players) Cockpit(authority, player, world);
            foreach (var player in players) authority.Submit(player, Select(authority, world, 1,
                Hero(world, player.PlayerSlot), planet: "competition-" + player.PlayerSlot));
            var results = authority.Tick();
            Assert.That(results.Count(r => r.Code == SessionResultCode.Applied), Is.EqualTo(1));
            Assert.That(results.Count(r => r.Code == SessionResultCode.NoEffect), Is.EqualTo(3));
            int winner = results.Single(r => r.Code == SessionResultCode.Applied).PlayerSlot;
            Assert.That(world.Journey.Capture().PlanetId, Is.EqualTo("competition-" + winner));
            Assert.That(Ship(world).CaptureState().PilotId, Is.EqualTo(Hero(world, winner).Id));
            await Until(authority, world, JourneyPhase.ArrivalSync);
            var map = world.Terrain.Map; string identity = world.Journey.Capture().MapId;
            for (int i = 0; i < 100; i++) authority.Tick();
            Assert.That(authority.Epoch, Is.EqualTo(2)); Assert.That(world.Terrain.Map, Is.SameAs(map));
            Assert.That(world.Terrain.Capture().WorldId, Is.EqualTo(identity));
        });

        [UnityTest]
        public IEnumerator DisabledPlanetUnknownPlanetStaleRevisionAndLeaseHaveNoSideEffects() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = JourneyScenario.Create(scope, c => c.Planets.Add(new PlanetPreset
            { Id = "disabled-planet", DisplayName = "停用星球", Enabled = false }));
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); Cockpit(authority, host, world);
            var hero = Hero(world, 0); int lease = hero.CaptureState().ControlLease, revision = world.Journey.Capture().Revision;
            Assert.That(Execute(authority, host, Select(authority, world, 1, hero, planet: "disabled-planet")), Is.EqualTo(SessionResultCode.NoEffect));
            Assert.That(Execute(authority, host, Select(authority, world, 2, hero, planet: "unknown-planet")), Is.EqualTo(SessionResultCode.NoEffect));
            Assert.That(Execute(authority, host, Select(authority, world, 3, hero, revision: revision - 1)), Is.EqualTo(SessionResultCode.NoEffect));
            Assert.That(Execute(authority, host, Select(authority, world, 4, hero, lease: lease + 1)), Is.EqualTo(SessionResultCode.PermissionDenied));
            Assert.That(world.Journey.Capture().Revision, Is.EqualTo(revision)); Assert.That(hero.CaptureState().ControlLease, Is.EqualTo(lease));
            Assert.That(Ship(world).CaptureState().PilotId, Is.Zero); Assert.That(Generation(world), Is.Null);
        });

        [UnityTest]
        public IEnumerator HostOnlyPolicyRejectsQueuedGuestSelectionAndRevokesGuestPilot() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); var guest = Connect(authority, 1);
            Cockpit(authority, guest, world);
            var guestRequest = Select(authority, world, 1, Hero(world, 1));
            authority.Submit(host, Policy(authority, 1, CampControlMode.HostOnly)); authority.Submit(guest, guestRequest);
            var results = authority.Tick();
            Assert.That(results.Single(r => r.PlayerSlot == 1).Code, Is.EqualTo(SessionResultCode.PolicyChanged));
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Orbit));
            Assert.That(Execute(authority, host, Policy(authority, 2, CampControlMode.SharedCamp)), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(Execute(authority, guest, Select(authority, world, 2, Hero(world, 1))), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(Ship(world).CaptureState().PilotId, Is.EqualTo(Hero(world, 1).Id));
            Assert.That(Execute(authority, host, Policy(authority, 3, CampControlMode.HostOnly)), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Orbit)); Assert.That(Ship(world).CaptureState().PilotId, Is.Zero);
            Assert.That(Hero(world, 1).CaptureState().ControllerSlot, Is.EqualTo(-1));
        });

        [UnityTest]
        public IEnumerator NonPilotCannotCancelAndConflictingSequenceCannotChangeDestination() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope,
                c => c.Planets.Add(new PlanetPreset { Id = "other-planet", DisplayName = "另一星球" }));
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); var guest = Connect(authority, 1);
            Cockpit(authority, host, world); var hero = Hero(world, 0);
            Execute(authority, host, Select(authority, world, 1, hero)); string destination = world.Journey.Capture().PlanetId;
            Assert.That(Execute(authority, guest, Cancel(authority, world, 1, Hero(world, 1))), Is.EqualTo(SessionResultCode.NoEffect));
            Assert.That(authority.Submit(host, Select(authority, world, 1, hero, planet: "other-planet")).Code,
                Is.EqualTo(SessionResultCode.SequenceConflict));
            Assert.That(world.Journey.Capture().PlanetId, Is.EqualTo(destination));
        });

        [UnityTest]
        public IEnumerator ThrowingArrivalSubscriberDoesNotDisposeOrReinstallActiveMap() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); Cockpit(authority, host, world);
            Action<string, bool> fail = (text, warning) =>
            { if (text.StartsWith("已到达 ", StringComparison.Ordinal)) throw new InvalidOperationException("injected arrival subscriber"); };
            world.Feedback.Message += fail;
            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: injected arrival subscriber"));
                Execute(authority, host, Select(authority, world, 1, Hero(world, 0)));
                await Until(authority, world, JourneyPhase.ArrivalSync);
                var map = world.Terrain.Map; string identity = world.Journey.Capture().MapId;
                for (int i = 0; i < 60; i++) authority.Tick();
                Assert.That(world.Terrain.Map, Is.SameAs(map)); Assert.That(authority.Epoch, Is.EqualTo(2));
                Assert.That(world.Terrain.Capture().WorldId, Is.EqualTo(identity)); Assert.That(world.Flow.TakeArrivalCandidate(), Is.Null);
            }
            finally { world.Feedback.Message -= fail; }
        });

        [UnityTest]
        public IEnumerator ChangingAuthorCloneCannotMutateAnAlreadyFrozenSession() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); Connect(authority, 0);
            var frozen = world.Journey.Capture();
            var source = world.Camp.Object.Definition.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            source.Planets[0].DisplayName = "会话开始后的作者改动";
            Assert.That(world.Flow.Accepts(frozen), Is.True);
            Assert.That(world.Journey.Capture().Planets[0].DisplayName, Is.EqualTo(frozen.Planets[0].DisplayName));
            Assert.That(world.Flow.ContentFingerprint, Is.Not.EqualTo(source.Fingerprint()));
        });

        [UnityTest]
        public IEnumerator RetiringSessionClearsFrozenCatalogAndIgnoresPendingGeneration() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            Cockpit(authority, host, world);
            Assert.That(Execute(authority, host, Select(authority, world, 1, Hero(world, 0))),
                Is.EqualTo(SessionResultCode.Applied));
            var pending = Generation(world); var flow = world.Flow;
            Assert.That(pending, Is.Not.Null); Assert.That(flow.Planets.Count, Is.GreaterThan(0));
            world.Camp.Object.Retire();
            Assert.That(flow.Enabled, Is.False); Assert.That(flow.Planets, Is.Empty);
            Assert.That(flow.ContentFingerprint, Is.Empty);
            Assert.That(flow.PreparationTimeoutSeconds, Is.Zero);
            Assert.That(flow.ArrivalTimeoutSeconds, Is.Zero);
            Assert.That(Generation(world), Is.Null);
            for (int i = 0; i < 1700 && !pending.IsCompleted; i++) await UniTask.Yield();
            Assert.That(pending.IsCompleted, Is.True);
            Assert.That(flow.TakeArrivalCandidate(), Is.Null);
            Assert.That(flow.Planets, Is.Empty);
        });

        private static SessionRequest Policy(SessionAuthority authority, long sequence, CampControlMode mode) =>
            new SessionRequest(SessionOperation.SetControlMode, SessionAuthority.ProtocolVersion, authority.Epoch,
                authority.PolicyRevision, sequence, value: (int)mode);
    }
}
