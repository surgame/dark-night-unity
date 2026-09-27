using System.Collections;
using Cysharp.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Session;
using NUnit.Framework;
using UnityEngine.TestTools;
using static DarkNights.Tests.ShipScenario;
using static DarkNights.Tests.JourneyScenario;

namespace DarkNights.Tests
{
    /// <summary>航程有界等待和晚加入边界；真实网络包、跨进程时延与资源可见性由 Player 验收补足。</summary>
    public sealed class JourneyRecoveryTests
    {
        [UnityTest]
        public IEnumerator PreparationTimeoutCancelsCandidateWithoutChangingEpoch() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); Cockpit(authority, host, world);
            Execute(authority, host, Select(authority, world, 1, Hero(world, 0)));
            // 只推进正式模拟时钟，以免把 30 秒超时测试变成真实等待；不改阶段或任务完成状态。
            world.Advance(world.Flow.PreparationTimeoutSeconds + 1);
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Orbit));
            StringAssert.Contains("超时", world.Journey.Capture().Error);
            Assert.That(Ship(world).CaptureState().PilotId, Is.Zero); Assert.That(authority.Epoch, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator ArrivalTimeoutLeavesSlowGuestUnreadyAndAllowsReadyHostToLand() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); var guest = Connect(authority, 1);
            Cockpit(authority, host, world); Execute(authority, host, Select(authority, world, 1, Hero(world, 0)));
            await Until(authority, world, JourneyPhase.ArrivalSync);
            Assert.That(authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true), Is.True);
            for (int i = 0; i < world.Flow.ArrivalTimeoutSeconds * 60 + 2; i++) authority.Tick();
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Descent)); Assert.That(guest.Ready, Is.False);
            Assert.That(Ship(world).CaptureState().ShipVelocityY, Is.InRange(-world.Catalog.Balance.Expedition.Ship.IdleDescentSpeed, 0));
            Assert.That(authority.AcknowledgeReady(guest, authority.Epoch, authority.Revision, true), Is.True);
            Assert.That(Hero(world, 1).CaptureState().Boarded, Is.True);
        });

        [UnityTest]
        public IEnumerator LateJoinInDescentSpawnsInsideShipAndCannotStealPilotSeat() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            await Descent(authority, world, host); var guest = Connect(authority, 1); var hero = Hero(world, 1);
            Assert.That(hero.CaptureState().Boarded, Is.True);
            Assert.That(hero.CaptureState().Height - Ship(world).CaptureState().Height, Is.EqualTo(40).Within(.01));
            Cockpit(authority, guest, world);
            Assert.That(Send(authority, guest, 1, "pilot", hero), Is.EqualTo(SessionResultCode.NoEffect));
            Assert.That(Ship(world).CaptureState().PilotId, Is.EqualTo(Hero(world, 0).Id));
        });
    }
}
