using System;
using DarkNights.Core.Config;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>验证真实运动的双向坡面前进、上升中再次接坡、点跳保留、策略与喷气衔接；不以最终落地代替途中流畅度。</summary>
    public sealed class TerrainJumpFlowTests
    {
        private const double Delta = 1.0 / 60;

        [TestCase(.5f, false, false)] [TestCase(.5f, true, false)]
        [TestCase(1f, false, false)] [TestCase(1f, true, false)]
        [TestCase(.5f, false, true)] [TestCase(.5f, true, true)]
        [TestCase(1f, false, true)] [TestCase(1f, true, true)]
        public void UphillJumpContinuesWithoutZeroHorizontalSteps(float slope, bool sprint, bool mirror)
        {
            var map = TerrainMotionTestMap.Ramp(slope, mirror);
            var rules = TerrainMotionTestMap.Rules();
            float start = mirror ? 5104 - 1760 : 1760;
            var state = TerrainMotionTestMap.Actor(start, TerrainMotionTestMap.BaseHeight + slope * 176);
            int direction = mirror ? -1 : 1;
            for (int tick = 0; tick < 60; tick++)
            {
                float before = state.X;
                TerrainHeroMotion.Tick(map, state, rules, Delta, tick == 0, false,
                    targetX: state.X + direction * rules.MoveSpeed(sprint) * (float)Delta);
                Assert.That(Math.Abs(state.X - before), Is.GreaterThan(.01f), "停顿 tick=" + tick);
            }
            Assert.That(Math.Abs(state.X - start), Is.GreaterThan(rules.MoveSpeed(sprint) - 3));
        }

        [Test]
        public void SteepSprintRecontactsBeforeWorldVerticalApex()
        {
            var map = TerrainMotionTestMap.Ramp(1);
            var rules = TerrainMotionTestMap.Rules();
            var state = TerrainMotionTestMap.Actor(1760, TerrainMotionTestMap.BaseHeight + 176);
            int landed = -1;
            for (int tick = 0; tick < 20; tick++)
            {
                TerrainHeroMotion.Tick(map, state, rules, Delta, tick == 0, false,
                    targetX: state.X + rules.MoveSpeed(true) * (float)Delta);
                if (tick == 0) Assert.That(state.SupportPlatform, Is.EqualTo(-1), "起跳首步必须真正离坡");
                if (tick > 0 && state.SupportPlatform >= 0) { landed = tick; break; }
            }
            Assert.That(landed, Is.InRange(1, 19));
            Assert.That(state.VerticalSpeed, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void RepeatedSlopePressesKeepHorizontalProgress(bool mirror)
        {
            var map = TerrainMotionTestMap.Ramp(1, mirror);
            var rules = TerrainMotionTestMap.Rules();
            float start = mirror ? 5104 - 1760 : 1760;
            var state = TerrainMotionTestMap.Actor(start, TerrainMotionTestMap.BaseHeight + 176);
            for (int tick = 0; tick < 60; tick++)
            {
                float before = state.X;
                TerrainHeroMotion.Tick(map, state, rules, Delta, tick % 6 == 0, false,
                    targetX: state.X + (mirror ? -1 : 1) * rules.MoveSpeed(true) * (float)Delta);
                Assert.That(Math.Abs(state.X - before), Is.GreaterThan(.01f));
            }
            Assert.That(Math.Abs(state.X - start), Is.GreaterThan(160));
        }

        [Test]
        public void FallingAcrossGapEstablishesSupportInTheContactStep()
        {
            var map = TerrainMotionTestMap.Ramp(0);
            for (int row = 100; row < 106; row++) map.Clear(99, row);
            var state = TerrainMotionTestMap.Actor(1584, TerrainMotionTestMap.BaseHeight + .05f);
            TerrainMotionTestMap.Set(state, nameof(ActorState.SupportPlatform), -1);
            TerrainMotionTestMap.Set(state, nameof(ActorState.VerticalSpeed), -20f);
            TerrainHeroMotion.Tick(map, state, TerrainMotionTestMap.Rules(), Delta, false, false, targetX: 1586);
            Assert.That(state.SupportPlatform, Is.Zero);
            Assert.That(state.Height, Is.EqualTo(TerrainMotionTestMap.BaseHeight).Within(.002));
            Assert.That(state.X, Is.EqualTo(1586).Within(.002));
        }

        [Test]
        public void LandingConsumesEarlyPressOnce()
        {
            var map = TerrainMotionTestMap.Ramp(0);
            var rules = TerrainMotionTestMap.Rules();
            var state = TerrainMotionTestMap.Actor(1480, TerrainMotionTestMap.BaseHeight + .3f);
            TerrainMotionTestMap.Set(state, nameof(ActorState.SupportPlatform), -1);
            TerrainMotionTestMap.Set(state, nameof(ActorState.VerticalSpeed), -8f);
            TerrainHeroMotion.Tick(map, state, rules, Delta, true, false);
            TerrainHeroMotion.Tick(map, state, rules, Delta, false, false);
            Assert.That(state.VerticalSpeed, Is.GreaterThan(100));
            Assert.That(state.JumpBufferRemaining, Is.Zero);
            for (int tick = 0; tick < 120; tick++) TerrainHeroMotion.Tick(map, state, rules, Delta, false, true);
            Assert.That(state.SupportPlatform, Is.Zero);
            Assert.That(state.Height, Is.EqualTo(TerrainMotionTestMap.BaseHeight).Within(.002));
        }

        [Test]
        public void AirPressExpiresWithoutGrantingAnotherJump()
        {
            var map = TerrainMotionTestMap.Ramp(0);
            var rules = TerrainMotionTestMap.Rules();
            var state = TerrainMotionTestMap.Actor(1480, TerrainMotionTestMap.BaseHeight + 20);
            TerrainMotionTestMap.Set(state, nameof(ActorState.SupportPlatform), -1);
            for (int tick = 0; tick < 120; tick++)
            {
                TerrainHeroMotion.Tick(map, state, rules, Delta, tick == 0, false);
                Assert.That(state.VerticalSpeed, Is.LessThanOrEqualTo(0));
            }
            Assert.That(state.JumpBufferRemaining, Is.Zero);
        }

        [Test]
        public void FixedStrategyIgnoresReleaseAndHoldStrategyPreservesFullJumpMaximum()
        {
            float fixedTap = Peak(HeroJumpStrategy.Fixed, false);
            float fixedHold = Peak(HeroJumpStrategy.Fixed, true);
            float variableTap = Peak(HeroJumpStrategy.HoldHeight, false);
            float variableHold = Peak(HeroJumpStrategy.HoldHeight, true);
            Assert.That(fixedTap, Is.EqualTo(fixedHold).Within(.002));
            Assert.That(variableHold, Is.EqualTo(fixedHold).Within(.002));
            Assert.That(variableTap, Is.LessThan(fixedHold * .5f));
        }

        [Test]
        public void JetpackWaitsForOrdinaryAscentAndDoesNotClampTheSecondStep()
        {
            var map = TerrainMotionTestMap.Ramp(0);
            var rules = TerrainMotionTestMap.Rules();
            var state = TerrainMotionTestMap.Actor(1480, TerrainMotionTestMap.BaseHeight, true);
            TerrainHeroMotion.Tick(map, state, rules, Delta, true, true);
            TerrainHeroMotion.Tick(map, state, rules, Delta, false, true);
            Assert.That(state.VerticalSpeed, Is.GreaterThan(140));
            Assert.That(state.JetpackFuel, Is.EqualTo(2));
            for (int tick = 0; tick < 35; tick++) TerrainHeroMotion.Tick(map, state, rules, Delta, false, true);
            Assert.That(state.JetpackFuel, Is.LessThan(2));
            Assert.That(state.VerticalSpeed, Is.GreaterThan(0));
        }

        private static float Peak(HeroJumpStrategy strategy, bool held)
        {
            var state = TerrainMotionTestMap.Actor(1480, TerrainMotionTestMap.BaseHeight);
            var map = TerrainMotionTestMap.Ramp(0); var rules = TerrainMotionTestMap.Rules(strategy);
            float peak = state.Height;
            for (int tick = 0; tick < 120; tick++)
            {
                TerrainHeroMotion.Tick(map, state, rules, Delta, tick == 0, held);
                peak = Math.Max(peak, state.Height);
            }
            return peak - TerrainMotionTestMap.BaseHeight;
        }
    }
}
