#if UNITY_EDITOR
namespace DarkNights.Runtime.Terrain
{
    /// <summary>一次权威地形运动的诊断值副本；只保存数值，不持有角色、地图或状态池引用。</summary>
    public struct TerrainMotionTraceFrame
    {
        internal bool Captured;
        public int Sequence, ActorId, SupportBefore, SupportAfter, LeftShape, RightShape;
        public ulong MapCommitBefore, MapCommitAfter;
        public double Delta, BufferBefore, BufferAfter;
        public double WallTime;
        public long InputSequence, InputReceivedTick, JumpSequence;
        public float XBefore, HeightBefore, SpeedBefore, XAfter, HeightAfter, SpeedAfter, TargetX;
        public float? GapBefore, GapAfter;
        public bool Pressed, Held, GroundedAtStart, JumpStarted, AscendingBefore, AscendingAfter, BodyBlocked;
    }
}
#endif
