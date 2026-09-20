using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Logic.State;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 经实际权威命令与输入流验证接管互斥、短按、平台、暂停以及道具生产，不直接写模拟字段。
    /// 职业速度、采集产量和原前摇保持现有配置；输入包重放不能额外推进能力。
    /// </summary>
    public sealed class HeroControlTests
    {
        [UnityTest]
        public IEnumerator ReadyCreatesDistinctVillagersForAllFourSlotsWithoutClaimingSceneActors() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create(true);
            SessionConnection third = f.Authority.Connect(2);
            SessionConnection fourth = f.Authority.Connect(3);
            Assert.That(f.Authority.AcknowledgeReady(third, f.Authority.Epoch, f.Authority.Revision, true), Is.True);
            Assert.That(f.Authority.AcknowledgeReady(fourth, f.Authority.Epoch, f.Authority.Revision, true), Is.True);
            var assigned = f.Authority.CaptureProjection().World.Actors.Where(actor => actor.ControllerSlot >= 0).ToArray();
            Assert.That(assigned.Length, Is.EqualTo(4));
            Assert.That(assigned.Select(actor => actor.ControllerSlot), Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
            Assert.That(assigned.Select(actor => actor.Id).Distinct().Count(), Is.EqualTo(4));
            Assert.That(assigned.All(actor => f.World.Index.Find<ActorBehaviour>(actor.Id).PlacementKey.Length == 0), Is.True);
            Assert.That(f.World.Index.Actors.Where(actor => actor.PlacementKey.Length != 0)
                .All(actor => actor.CaptureState().ControllerSlot < 0), Is.True);
            int count = f.World.Index.Actors.Count;
            int[] ids = assigned.Select(actor => actor.Id).OrderBy(id => id).ToArray();
            f.Ready();
            Assert.That(f.World.Index.Actors.Count, Is.EqualTo(count));
            Assert.That(f.Authority.CaptureProjection().World.Actors.Where(actor => actor.ControllerSlot >= 0)
                .Select(actor => actor.Id).OrderBy(id => id), Is.EqualTo(ids));
            Assert.That(f.State.ControllerSlot, Is.Zero);
        });

        [UnityTest]
        public IEnumerator ReconnectCreatesANewVillagerInsteadOfTakingReleasedOne() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create(true);
            int oldId = f.World.Index.Actors.Single(actor => actor.CaptureState().ControllerSlot == 1).Id;
            int count = f.World.Index.Actors.Count;
            f.Authority.Disconnect(f.Guest, false);
            SessionConnection replacement = f.Authority.Connect(1);
            Assert.That(f.Authority.AcknowledgeReady(replacement, f.Authority.Epoch, f.Authority.Revision, true), Is.True);
            int newId = f.World.Index.Actors.Single(actor => actor.CaptureState().ControllerSlot == 1).Id;
            Assert.That(newId, Is.Not.EqualTo(oldId));
            Assert.That(f.World.Index.Find<ActorBehaviour>(oldId).CaptureState().ControllerSlot, Is.EqualTo(-1));
            Assert.That(f.World.Index.Actors.Count, Is.EqualTo(count + 1));
        });

        [UnityTest]
        public IEnumerator SharedCampRestoresTheSameGeneratedVillager() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create(true);
            int guestId = f.World.Index.Actors.Single(actor => actor.CaptureState().ControllerSlot == 1).Id;
            int count = f.World.Index.Actors.Count;
            Assert.That(f.Command(SessionOperation.SetControlMode, value: 1).Code, Is.EqualTo(SessionResultCode.Applied));
            Assert.That(f.World.Index.Find<ActorBehaviour>(guestId).CaptureState().ControllerSlot, Is.EqualTo(-1));
            Assert.That(f.Command(SessionOperation.SetControlMode, value: 0).Code, Is.EqualTo(SessionResultCode.Applied));
            Assert.That(f.World.Index.Actors.Single(actor => actor.CaptureState().ControllerSlot == 1).Id, Is.EqualTo(guestId));
            Assert.That(f.World.Index.Actors.Count, Is.EqualTo(count));
        });

        [UnityTest]
        public IEnumerator PossessionCancelsOrdersAndRejectsOtherConnection() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            var site = f.World.Index.Worksites[0];
            f.World.IssueOrders(new[] { f.ActorId }, site.Id, site.X);
            Assert.That(f.Command(SessionOperation.ClaimHero).Code, Is.EqualTo(SessionResultCode.Applied));
            Assert.That(site.WorkerId, Is.Zero); Assert.That(f.State.TargetId, Is.Zero);
            Assert.That(f.Command(SessionOperation.ClaimHero, f.Guest).Code, Is.EqualTo(SessionResultCode.NoEffect));
            Assert.That(f.Authority.SubmitInput(f.Guest, f.Packet(horizontal: 1)), Is.False);
            Assert.That(f.World.IssueOrders(new[] { f.ActorId }, 0, 800), Is.Zero);
            float start = f.Actor.X;
            Assert.That(f.Input(horizontal: 1), Is.True); f.Step(6);
            Assert.That(f.Actor.X, Is.EqualTo(start + 3).Within(.001), "Uses original 30 px/s worker speed.");
            Assert.That(f.State.Activity, Is.EqualTo(ActorActivity.Idle));
            Assert.That(f.World.Index.Actors[1].X, Is.EqualTo(187));
        });

        [UnityTest]
        public IEnumerator DebugSpeedMultipliesOnlyAuthoritativeHeroMovement() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create(debugHeroSpeedMultiplier: 8);
            Assert.That(f.Command(SessionOperation.ClaimHero).Code, Is.EqualTo(SessionResultCode.Applied));
            float start = f.Actor.X;
            Assert.That(f.Input(horizontal: 1), Is.True); f.Step(6);
            Assert.That(f.Actor.X, Is.EqualTo(start + 24).Within(.001));
            Assert.That(f.World.Speed, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator ShortJumpIsConsumedOnceAndSOnlyDropsCurrentPlatform() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            f.Command(SessionOperation.ClaimHero);
            var pressed = f.Packet(jumpHeld: true, jumpPressed: true);
            Assert.That(f.Authority.SubmitInput(f.Host, pressed), Is.True);
            f.Input(); f.Step(1);
            Assert.That(f.State.Height, Is.GreaterThan(0));
            Assert.That(f.State.JumpPending, Is.False);
            Assert.That(f.Authority.SubmitInput(f.Host, pressed), Is.False);
            f.Step(100);
            Assert.That(f.State.Height, Is.EqualTo(12)); Assert.That(f.State.SupportPlatform, Is.EqualTo(1));
            f.Input(dropPressed: true); f.Step(1);
            Assert.That(f.State.Height, Is.LessThan(12));
            f.Step(80); Assert.That(f.State.Height, Is.Zero); Assert.That(f.State.SupportPlatform, Is.Zero);
            f.Input(dropPressed: true); f.Step(10); Assert.That(f.State.Height, Is.Zero);
        });

        [UnityTest]
        public IEnumerator InputExpiryPauseAndDisconnectInvalidateOldControl() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            f.Command(SessionOperation.ClaimHero); f.Input(horizontal: 1);
            f.Step(SessionHeroControl.InputTimeoutTicks + 2);
            float stopped = f.Actor.X; f.Step(20); Assert.That(f.Actor.X, Is.EqualTo(stopped));
            var old = f.Packet(horizontal: 1, jumpPressed: true);
            f.Command(SessionOperation.SetPaused, value: 1);
            Assert.That(f.Authority.SubmitInput(f.Host, old), Is.False);
            f.Command(SessionOperation.SetPaused, value: 0);
            Assert.That(f.Authority.SubmitInput(f.Host, old), Is.False);
            f.Authority.Disconnect(f.Host, false);
            Assert.That(f.State.ControllerSlot, Is.EqualTo(-1)); Assert.That(f.State.ManualControl, Is.False);
            Assert.That(f.Authority.SubmitInput(f.Host, f.Packet(horizontal: 1)), Is.False);
        });

        [UnityTest]
        public IEnumerator SelectionVersionAndFuelRemainAuthoritative() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            f.Command(SessionOperation.ClaimHero);
            f.Command(SessionOperation.SelectHeroItem, value: 3);
            Assert.That(f.Command(SessionOperation.UseHeroItem, kind: "jetpack", value: 0).Code, Is.EqualTo(SessionResultCode.NoEffect));
            Assert.That(f.State.JetpackEquipped, Is.False);
            Assert.That(f.Command(SessionOperation.UseHeroItem, kind: "jetpack", value: f.State.SelectionRevision).Code, Is.EqualTo(SessionResultCode.Applied));
            f.Input(jumpHeld: true, jumpPressed: true); f.Step(1);
            f.Step(90, jumpHeld: true, keepAlive: true);
            Assert.That(f.State.Height, Is.GreaterThan(22));
            Assert.That(f.State.JetpackFuel, Is.InRange(0, .6));
            f.Step(150); Assert.That(f.State.JetpackFuel, Is.GreaterThan(0));
            Assert.That(f.State.VerticalSpeed, Is.Zero);
        });

        [UnityTest]
        public IEnumerator PickaxeOnlyAnimatesWithoutAssigningWorkOrProducingResources() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            f.Command(SessionOperation.ClaimHero); f.Command(SessionOperation.SelectHeroItem, value: 1);
            var site = f.World.Index.Worksites.First(w => w.RuleKey == "wood");
            double before = f.World.Economy.Stock.Wood;
            f.Input(useHeld: true); f.Step(4);
            Assert.That(f.State.EquipmentAction, Is.GreaterThan(0));
            Assert.That(site.WorkerId, Is.Zero);
            f.Step(90, useHeld: true, keepAlive: true);
            Assert.That(f.World.Economy.Stock.Wood, Is.EqualTo(before));
            f.Input(); f.Step(40);
            Assert.That(f.State.EquipmentAction, Is.Zero);
        });

        [UnityTest]
        public IEnumerator PolicyChangeReleasesGuestAndRejectsQueuedLease() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            f.Command(SessionOperation.ClaimHero, f.Guest);
            var old = f.Packet(horizontal: 1);
            f.Command(SessionOperation.SetControlMode, value: 1);
            Assert.That(f.State.ControllerSlot, Is.EqualTo(-1));
            Assert.That(f.Authority.SubmitInput(f.Guest, old), Is.False);
            Assert.That(f.Command(SessionOperation.ClaimHero, f.Guest).Code, Is.EqualTo(SessionResultCode.PermissionDenied));
            Assert.That(f.Command(SessionOperation.ClaimHero).Code, Is.EqualTo(SessionResultCode.Applied));
        });
    }
}
