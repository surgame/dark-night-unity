using DarkNights.Runtime.Terrain;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>验证诊断记录器只读观察真实运动、限定角色、冻结副本与容量停止，不把近地标记当作玩法结论。</summary>
    public sealed class TerrainMotionTraceTests
    {
        [TearDown]
        public void Stop() => TerrainMotionTrace.Stop();

        [Test]
        public void RecordingDoesNotChangeMotionAndSnapshotIsFrozen()
        {
            var map = TerrainMotionTestMap.Ramp(.5f);
            var rules = TerrainMotionTestMap.Rules();
            var observed = TerrainMotionTestMap.Actor(1760, TerrainMotionTestMap.BaseHeight + 88);
            var baseline = TerrainMotionTestMap.Actor(1760, TerrainMotionTestMap.BaseHeight + 88);
            TerrainMotionTrace.Stop();
            TerrainHeroMotion.Tick(map, baseline, rules, 1d / 60, true, false, targetX: 1762);
            TerrainMotionTrace.Start(observed.Id);
            TerrainHeroMotion.Tick(map, observed, rules, 1d / 60, true, false, targetX: 1762);
            Assert.That(observed.X, Is.EqualTo(baseline.X));
            Assert.That(observed.Height, Is.EqualTo(baseline.Height));
            Assert.That(observed.VerticalSpeed, Is.EqualTo(baseline.VerticalSpeed));
            Assert.That(observed.SupportPlatform, Is.EqualTo(baseline.SupportPlatform));
            var snapshot = TerrainMotionTrace.Snapshot();
            Assert.That(snapshot.Length, Is.EqualTo(1));
            Assert.That(snapshot[0].JumpStarted, Is.True);
            Assert.That(TerrainMotionTrace.TryRead(0, out var first), Is.True);
            Assert.That(first.JumpStarted, Is.True);
            Assert.That(TerrainMotionTrace.TryRead(-1, out _), Is.False);
            Assert.That(TerrainMotionTrace.TryRead(1, out _), Is.False);
            TerrainHeroMotion.Tick(map, observed, rules, 1d / 60, false, false);
            Assert.That(snapshot.Length, Is.EqualTo(1));
            snapshot[0].Sequence = 999;
            Assert.That(TerrainMotionTrace.Snapshot()[0].Sequence, Is.EqualTo(1));
        }

        [Test]
        public void WalkingOffStepRecordsSupportLossWithoutJump()
        {
            var map = TerrainMotionTestMap.Ramp(0);
            for (int column = 80; column < 100; column++) map.Set(column, 99);
            var actor = TerrainMotionTestMap.Actor(1598, TerrainMotionTestMap.BaseHeight + 16);
            TerrainMotionTrace.Start(actor.Id);
            TerrainHeroMotion.Tick(map, actor, TerrainMotionTestMap.Rules(), 1d / 60, false, false, targetX: 1602);
            var frame = TerrainMotionTrace.Snapshot()[0];
            Assert.That(frame.GroundedAtStart, Is.True);
            Assert.That(frame.JumpStarted, Is.False);
            Assert.That(frame.SupportBefore, Is.Zero);
            Assert.That(frame.SupportAfter, Is.EqualTo(-1));
            Assert.That(frame.GapAfter, Is.EqualTo(16).Within(.002));
        }

        [Test]
        public void OtherActorsAreIgnoredAndCapacityStopsRecording()
        {
            var map = TerrainMotionTestMap.Ramp(0);
            var actor = TerrainMotionTestMap.Actor(1480, TerrainMotionTestMap.BaseHeight);
            var rules = TerrainMotionTestMap.Rules();
            TerrainMotionTrace.Start(actor.Id + 1);
            TerrainHeroMotion.Tick(map, actor, rules, 1d / 60, false, false);
            Assert.That(TerrainMotionTrace.Count, Is.Zero);
            TerrainMotionTrace.Start(actor.Id);
            for (int i = 0; i < TerrainMotionTrace.Capacity + 2; i++)
                TerrainHeroMotion.Tick(map, actor, rules, 1d / 60, false, false);
            Assert.That(TerrainMotionTrace.Count, Is.EqualTo(TerrainMotionTrace.Capacity));
            Assert.That(TerrainMotionTrace.Recording, Is.False);
            Assert.That(TerrainMotionTrace.StopReason, Does.Contain("上限"));
        }
    }
}
