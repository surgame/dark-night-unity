using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Session;
using NUnit.Framework;
using UnityEngine.TestTools;
using static DarkNights.Tests.ShipScenario;
using static DarkNights.Tests.JourneyScenario;

namespace DarkNights.Tests
{
    /// <summary>不同航程阶段的合法出生和高地表真实通行验收；不注入角色坐标、速度或登船状态。</summary>
    public sealed class JourneyPlacementTests
    {
        [UnityTest]
        public IEnumerator PreparingTransitAndArrivalLateJoinRetainEveryCrewMember() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); Cockpit(authority, host, world);
            Execute(authority, host, Select(authority, world, 1, Hero(world, 0)));
            var preparing = Connect(authority, 1);
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Preparing)); Assert.That(Hero(world, 1).CaptureState().Boarded, Is.True);
            await Until(authority, world, JourneyPhase.Transit); var transit = Connect(authority, 2);
            Assert.That(Hero(world, 2).CaptureState().Boarded, Is.True);
            await Until(authority, world, JourneyPhase.ArrivalSync); var arriving = Connect(authority, 3);
            int[] ids = world.Index.Actors.Select(a => a.Id).ToArray();
            foreach (var player in new[] { host, preparing, transit, arriving })
                Assert.That(authority.AcknowledgeReady(player, authority.Epoch, authority.Revision, true), Is.True);
            authority.Tick();
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Descent));
            CollectionAssert.AreEquivalent(ids, world.Index.Actors.Select(a => a.Id)); Assert.That(ids.Length, Is.EqualTo(4));
            Assert.That(world.CaptureView().Expedition.Crew.All(c => c.Boarded), Is.True);
        });

        [UnityTest]
        public IEnumerator HighPlatformLandingExitJumpAndBoardUsePlanetGroundHeight() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = JourneyScenario.Create(scope, c => { c.Planets[0].DockRow = 28; c.Planets[0].MaximumLift = 256; });
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            await Descent(authority, world, host); Land(authority, world, host);
            var hero = Hero(world, 0); var ship = Ship(world);
            Assert.That(ship.CaptureState().DockHeight, Is.EqualTo(192));
            Walk(authority, host, hero, ship.X + ShipGeometry.RampToe - 12);
            Assert.That(hero.CaptureState().Boarded, Is.False); Assert.That(hero.CaptureState().Height, Is.EqualTo(192).Within(1));
            var state = hero.CaptureState();
            Assert.That(authority.SubmitInput(host, new HeroInputRequest(SessionAuthority.ProtocolVersion, authority.Epoch,
                authority.PolicyRevision, hero.Id, state.ControlLease, authority.ServerTick + 1, authority.ServerTick,
                0, true, false, true, false)), Is.True);
            authority.Tick();
            Assert.That(hero.CaptureState().Height, Is.GreaterThan(192));
            for (int i = 0; i < 180; i++) Input(authority, host, hero);
            Assert.That(hero.CaptureState().Height, Is.EqualTo(192).Within(1));
            Walk(authority, host, hero, ship.X + ShipGeometry.HoldX); Assert.That(hero.CaptureState().Boarded, Is.True);
        });

        [UnityTest]
        public IEnumerator LandedSaveRestoreAndLateJoinDoNotRestartGroundOrDuplicateCrew() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            await Descent(authority, world, host); Land(authority, world, host);
            string json = world.SaveCodec.Serialize(world.CaptureWorld()); world.Restore(json);
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Landed)); Assert.That(world.CaptureView().Expedition.Run, Is.EqualTo(1));
            host = Connect(authority, 0); var guest = Connect(authority, 1); int count = world.Index.Actors.Count();
            Assert.That(Hero(world, 1).CaptureState().Boarded, Is.True);
            Assert.That(authority.AcknowledgeReady(guest, authority.Epoch, authority.Revision, true), Is.True);
            Assert.That(world.Index.Actors.Count(), Is.EqualTo(count));
            Assert.That(world.CaptureView().Expedition.Phase, Is.EqualTo(1));
        });
    }
}
