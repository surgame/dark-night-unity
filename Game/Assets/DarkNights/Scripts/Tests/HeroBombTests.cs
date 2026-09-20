using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Session;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 蓄力、取消、黏附和恢复的待执行回归；时钟与位置均经真实会话推进，不直接改写道具状态。
    /// 这些用例随本切片编译，但必须在后续冒烟阶段执行后才能记录通过。
    /// </summary>
    public sealed class HeroBombTests
    {
        [UnityTest]
        public IEnumerator ThrowConsumesOneChargeAndEmptyInventoryCannotStartCharging() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            f.Command(SessionOperation.ClaimHero); f.Command(SessionOperation.SelectHeroItem, value: 2);
            for (int count = 3; count > 0; count--)
            {
                var packet = f.Packet(usePressed: true, useReleased: true);
                Assert.That(f.Authority.SubmitInput(f.Host, packet), Is.True);
                Assert.That(f.Authority.SubmitInput(f.Host, packet), Is.False);
                f.Step(1);
                Assert.That(f.State.ExplosiveCharges, Is.EqualTo(count - 1));
                f.Step(45);
            }
            long sequence = f.World.Projectiles.CaptureState().NextViewId;
            f.Authority.SubmitInput(f.Host, f.Packet(useHeld: true, usePressed: true)); f.Step(2);
            Assert.That(f.State.Charging, Is.False);
            Assert.That(f.World.Projectiles.CaptureState().NextViewId, Is.EqualTo(sequence));
            var frozen = f.Authority.CaptureWorld().Actors.Single(a => a.Id == f.ActorId);
            Assert.That(frozen.ExplosiveCharges, Is.Zero);
        });

        [UnityTest]
        public IEnumerator LongerChargeProducesHigherThrowSpeed() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            f.Command(SessionOperation.ClaimHero); f.Command(SessionOperation.SelectHeroItem, value: 2);
            f.Authority.SubmitInput(f.Host, f.Packet(usePressed: true, useReleased: true)); f.Step(1);
            float tap = f.World.Projectiles.CaptureState().Ballistics.Single(p => p.Kind == 2).VelocityX;
            f.Step(45);
            f.Authority.SubmitInput(f.Host, f.Packet(useHeld: true, usePressed: true));
            f.Step(60, useHeld: true, keepAlive: true);
            Assert.That(f.State.Charging, Is.True);
            f.Authority.SubmitInput(f.Host, f.Packet(useReleased: true)); f.Step(1);
            var charged = f.World.Projectiles.CaptureState().Ballistics.Where(p => p.Kind == 2).OrderByDescending(p => p.ViewId).First();
            Assert.That(charged.VelocityX, Is.GreaterThan(tap));
            Assert.That(f.State.Charging, Is.False);
        });

        [UnityTest]
        public IEnumerator TimeoutAndExplicitCancelNeverThrow() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            f.Command(SessionOperation.ClaimHero); f.Command(SessionOperation.SelectHeroItem, value: 2);
            f.Authority.SubmitInput(f.Host, f.Packet(useHeld: true, usePressed: true)); f.Step(10);
            f.Authority.SubmitInput(f.Host, f.Packet(cancelUse: true, useReleased: true)); f.Step(1);
            Assert.That(f.State.Charging, Is.False);
            f.Authority.SubmitInput(f.Host, f.Packet(useHeld: true, usePressed: true)); f.Step(40);
            Assert.That(f.State.Charging, Is.False);
            Assert.That(f.World.Projectiles.CaptureState().Ballistics.All(p => p.Kind == 0), Is.True);
        });

        [UnityTest]
        public IEnumerator GroundAdhesionAndFuseSurviveAtomicSaveRestore() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            f.Command(SessionOperation.ClaimHero); f.Command(SessionOperation.SelectHeroItem, value: 2);
            f.Authority.SubmitInput(f.Host, f.Packet(aim: -90, usePressed: true, useReleased: true)); f.Step(20);
            var stuck = f.World.Projectiles.CaptureState().Ballistics.Single(p => p.Kind == 2);
            Assert.That(stuck.Stuck, Is.True);
            string save = f.World.SaveCodec.Serialize(f.Authority.CaptureWorld());
            var ticket = f.Command(SessionOperation.BeginLoad); f.Authority.CompleteLoad(ticket, save); f.Ready();
            var restored = f.World.Projectiles.CaptureState().Ballistics.Single(p => p.Kind == 2);
            Assert.That(restored.X, Is.EqualTo(stuck.X));
            Assert.That(restored.Age, Is.EqualTo(stuck.Age));
            Assert.That(restored.Stuck, Is.True);
            f.Step(210);
            Assert.That(f.World.Projectiles.CaptureState().Ballistics.All(p => p.Kind == 0), Is.True);
        });

        [UnityTest]
        public IEnumerator SelectionAndPauseCancelCharge() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            f.Command(SessionOperation.ClaimHero); f.Command(SessionOperation.SelectHeroItem, value: 2);
            f.Authority.SubmitInput(f.Host, f.Packet(useHeld: true, usePressed: true)); f.Step(5);
            f.Command(SessionOperation.SelectHeroItem, value: 0);
            Assert.That(f.State.Charging, Is.False);
            f.Command(SessionOperation.SelectHeroItem, value: 2);
            f.Authority.SubmitInput(f.Host, f.Packet(useHeld: true, usePressed: true)); f.Step(5);
            f.Command(SessionOperation.SetPaused, value: 1); f.Step(1);
            Assert.That(f.State.Charging, Is.False);
            Assert.That(f.World.Projectiles.CaptureState().Ballistics.All(p => p.Kind == 0), Is.True);
        });
    }
}
