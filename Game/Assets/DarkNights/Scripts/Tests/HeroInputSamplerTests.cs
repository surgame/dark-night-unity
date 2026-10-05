using System;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using DarkNights.Entry;
using DarkNights.View;
using NUnit.Framework;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>验证跳跃边沿在发送间隔内立即发出且只消费一次，持续方向节流和输入权限失效仍保持原合同。</summary>
    public sealed class HeroInputSamplerTests
    {
        private GameObject root;
        private HeroInputSampler sampler;
        private SessionEntityViews visuals;
        private ActorViewData actor;
        private SessionViewData frame;

        [SetUp]
        public void Prepare()
        {
            root = new GameObject("HeroInputSamplerTests");
            visuals = root.AddComponent<SessionEntityViews>();
            sampler = new HeroInputSampler(root.AddComponent<Camera>(), 20);
            actor = new ActorViewData(1, "worker", "Test", false, 100, 25, "Idle", 0, 1, false, 0, 0, 0,
                manualControl: true, controllerSlot: 0, controlLease: 1);
            var camp = new CampViewData(new ResourceAmounts(), 1, 10, 0, 0, "Day", 100, 0, "Playing", 0, 0, new ResourceAmounts());
            var world = new WorldViewData(camp, new[] { actor }, Array.Empty<BuildingViewData>(),
                Array.Empty<WorksiteViewData>(), Array.Empty<ProjectileViewData>());
            frame = new SessionViewData(1, 1, 0, 0, 0, false, 1, 1, false, false, 1, 0, world);
        }

        [TearDown]
        public void Release() => UnityEngine.Object.DestroyImmediate(root);

        [Test]
        public void ShortPressBypassesSendIntervalAndIsNotResentByHeartbeat()
        {
            Assert.That(Sample(Controls(1), 0, out _), Is.True);
            Assert.That(Sample(Controls(1, pressed: true), .01, out var jump), Is.True);
            Assert.That(jump.JumpPressed, Is.True);
            Assert.That(jump.JumpHeld, Is.False, "同帧短按松开仍传递一次起跳边沿。");
            Assert.That(Sample(Controls(1), .02, out _), Is.False);
            Assert.That(Sample(Controls(1), .2, out var heartbeat), Is.True);
            Assert.That(heartbeat.JumpPressed, Is.False);
        }

        [Test]
        public void DirectionChangeWithoutJumpStillWaitsForThirtyHertzInterval()
        {
            Assert.That(Sample(Controls(1), 0, out _), Is.True);
            Assert.That(Sample(Controls(-1), .01, out _), Is.False);
            Assert.That(Sample(Controls(-1), .034, out var change), Is.True);
            Assert.That(change.Direction, Is.EqualTo(-1));
            Assert.That(change.JumpPressed, Is.False);
        }

        [Test]
        public void BlockedPressCannotLeakIntoRestoredControl()
        {
            Assert.That(Sample(Controls(1), 0, out _), Is.True);
            Sample(Controls(1, pressed: true, allowed: false), .01, out _);
            Assert.That(Sample(Controls(1), .2, out var resumed), Is.True);
            Assert.That(resumed.JumpPressed, Is.False);
        }

        private bool Sample(GameInputActions.HeroFrame controls, double now, out HeroInputSampler.Packet packet) =>
            sampler.Sample(controls, actor, frame, visuals, false, now, out packet);

        private static GameInputActions.HeroFrame Controls(float direction, bool pressed = false, bool allowed = true)
        {
            object boxed = default(GameInputActions.HeroFrame);
            var type = typeof(GameInputActions.HeroFrame);
            type.GetField("Allowed").SetValue(boxed, allowed);
            type.GetField("UseAllowed").SetValue(boxed, allowed);
            type.GetField("Move").SetValue(boxed, direction);
            type.GetField("JumpPressed").SetValue(boxed, pressed);
            type.GetField("ItemPressed").SetValue(boxed, -1);
            return (GameInputActions.HeroFrame)boxed;
        }
    }
}
