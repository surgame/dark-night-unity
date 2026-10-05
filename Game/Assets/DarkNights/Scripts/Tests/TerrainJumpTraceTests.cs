using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>验证诊断按输入序号关联阶段、保留显示与权威位置差、限制事件容量并在世界切换时停止。</summary>
    public sealed class TerrainJumpTraceTests
    {
        [TearDown]
        public void Stop() => TerrainMotionTrace.Stop();

        [Test]
        public void PressSendReceiveAndProcessShareSequenceAndFrozenValues()
        {
            var map = TerrainMotionTestMap.Ramp(0);
            var state = TerrainMotionTestMap.Actor(1480, TerrainMotionTestMap.BaseHeight);
            TerrainMotionTrace.Start(state.Id, 3);
            TerrainJumpTrace.CapturePress(map, state, 3, 100, 94, state.X, state.Height, state.X, state.Height);
            TerrainJumpTrace.Sent(state.Id, 7, 3, 94);
            var request = new HeroInputRequest(25, 3, 1, state.Id, 1, 7, 94, 0, true, false, true, false);
            TerrainJumpTrace.Received(map, state, request, 100);
            TerrainMotionTestMap.Set(state, nameof(state.LastInputSequence), 7L);
            TerrainHeroMotion.Tick(map, state, TerrainMotionTestMap.Rules(), 1d / 60, true, false);
            Assert.That(TerrainJumpTrace.Count, Is.EqualTo(4));
            foreach (string phase in new[] { "press", "send", "receive", "process" })
                Assert.That(TerrainJumpTrace.Find(phase, 7, out _), Is.True, phase);
            Assert.That(TerrainJumpTrace.Find("press", 7, out var press), Is.True);
            Assert.That(press.Gap, Is.Zero.Within(.002));
            Assert.That(press.VisualGap, Is.Zero.Within(.002));
            Assert.That(press.Geometry, Is.Not.Empty);
            var copy = TerrainJumpTrace.Snapshot(); copy[0].Phase = "changed";
            Assert.That(TerrainJumpTrace.Snapshot()[0].Phase, Is.EqualTo("press"));
            Assert.That(TerrainMotionTrace.Snapshot()[0].JumpSequence, Is.EqualTo(7));
        }

        [Test]
        public void VisualGroundAndAuthorityAirAreCapturedAtTheSamePress()
        {
            var map = TerrainMotionTestMap.Ramp(0);
            var state = TerrainMotionTestMap.Actor(1480, TerrainMotionTestMap.BaseHeight + 32);
            TerrainMotionTrace.Start(state.Id);
            TerrainJumpTrace.CapturePress(map, state, 1, 100, 94, state.X, TerrainMotionTestMap.BaseHeight, state.X, state.Height);
            var press = TerrainJumpTrace.Snapshot()[0];
            Assert.That(press.Gap, Is.EqualTo(32).Within(.002));
            Assert.That(press.VisualGap, Is.Zero.Within(.002));
            TerrainHeroMotion.Tick(map, state, TerrainMotionTestMap.Rules(), 1d / 60, false, false);
            Assert.That(TerrainMotionTrace.Snapshot()[0].GapBefore, Is.EqualTo(32).Within(.002));
        }

        [Test]
        public void EventsAreBoundedAndEpochChangesStopRecording()
        {
            TerrainMotionTrace.Start(2, 3);
            TerrainJumpTrace.Sent(99, 1, 3, 1);
            Assert.That(TerrainJumpTrace.Count, Is.Zero);
            for (int i = 0; i < TerrainJumpTrace.Capacity + 2; i++) TerrainJumpTrace.Sent(2, i + 1, 3, 1);
            Assert.That(TerrainJumpTrace.Count, Is.EqualTo(TerrainJumpTrace.Capacity));
            Assert.That(TerrainJumpTrace.Dropped, Is.EqualTo(2));
            TerrainJumpTrace.Sent(2, 2000, 4, 1);
            Assert.That(TerrainMotionTrace.Recording, Is.False);
            Assert.That(TerrainMotionTrace.StopReason, Does.Contain("世界代次"));
        }
    }
}
