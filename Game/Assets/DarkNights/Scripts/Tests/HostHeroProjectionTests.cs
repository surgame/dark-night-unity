using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>以真实权威会话验证Host主角即时展示的冻结性、空中状态及身份失效；投影只读，不推进模拟或修改旧帧。</summary>
    public sealed class HostHeroProjectionTests
    {
        [UnityTest]
        public IEnumerator LatestPoseDoesNotWaitForWorldPublicationAndRemainsFrozen() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create(true);
            var frame = f.Authority.CaptureProjection();
            var observed = frame.World.Actors.Single(a => a.Id == f.ActorId);
            float originalX = observed.X;
            f.Input(horizontal: 1); f.Step(6);
            var current = HostHeroProjection.Capture(f.World, f.Authority, frame, observed, f.Host.PlayerSlot);
            Assert.That(current, Is.Not.Null);
            Assert.That(current.X, Is.EqualTo(f.State.X));
            Assert.That(current.X, Is.GreaterThan(originalX));
            Assert.That(observed.X, Is.EqualTo(originalX), "网络副本不被即时展示改写。");
            float capturedX = current.X;
            int revision = f.Authority.Revision;
            HostHeroProjection.Capture(f.World, f.Authority, frame, observed, f.Host.PlayerSlot);
            Assert.That(f.Authority.Revision, Is.EqualTo(revision), "只读投影不提交业务事务。");
            f.Step(6);
            Assert.That(current.X, Is.EqualTo(capturedX), "展示副本不引用继续变化的ActorState。");
        });

        [UnityTest]
        public IEnumerator AirborneHeightAndSupportReplaceOldGroundedView() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create(true);
            var frame = f.Authority.CaptureProjection();
            var observed = frame.World.Actors.Single(a => a.Id == f.ActorId);
            Assert.That(observed.SupportPlatform, Is.Zero);
            f.Input(jumpHeld: true, jumpPressed: true); f.Step(1);
            var current = HostHeroProjection.Capture(f.World, f.Authority, frame, observed, f.Host.PlayerSlot);
            Assert.That(current, Is.Not.Null);
            Assert.That(current.Height, Is.EqualTo(f.State.Height));
            Assert.That(current.Height, Is.GreaterThan(observed.Height));
            Assert.That(current.SupportPlatform, Is.EqualTo(-1));
            Assert.That(observed.SupportPlatform, Is.Zero);
        });

        [UnityTest]
        public IEnumerator ForeignPlayerRevokedLeaseAndOtherEpochCannotUseImmediateView() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create(true);
            var frame = f.Authority.CaptureProjection();
            var observed = frame.World.Actors.Single(a => a.Id == f.ActorId);
            Assert.That(HostHeroProjection.Capture(f.World, f.Authority, frame, observed, f.Guest.PlayerSlot), Is.Null);
            f.Command(SessionOperation.ReleaseHero);
            Assert.That(HostHeroProjection.Capture(f.World, f.Authority, frame, observed, f.Host.PlayerSlot), Is.Null);
            f.Command(SessionOperation.ClaimHero);
            Assert.That(HostHeroProjection.Capture(f.World, f.Authority, frame, observed, f.Host.PlayerSlot), Is.Null);
            frame = f.Authority.CaptureProjection(); observed = frame.World.Actors.Single(a => a.Id == f.ActorId);
            Assert.That(HostHeroProjection.Capture(f.World, f.Authority, frame, observed, f.Host.PlayerSlot), Is.Not.Null);
            var other = new SessionViewData(frame.Publication, frame.Epoch + 1, frame.Revision, frame.ServerTick,
                frame.PolicyRevision, frame.HostOnly, frame.PlayerCount, frame.ReadyCount, false, frame.Paused,
                frame.Speed, frame.Elapsed, frame.World);
            Assert.That(HostHeroProjection.Capture(f.World, f.Authority, other, observed, f.Host.PlayerSlot), Is.Null);
        });
    }
}
