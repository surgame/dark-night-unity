#if UNITY_EDITOR
namespace DarkNights.Runtime.Terrain
{
    /// <summary>一次跳跃输入阶段的冻结诊断值；显示与权威坐标独立保存，几何片段为自有不可变字符串，不引用状态池。</summary>
    public struct TerrainJumpTraceSample
    {
        public string Phase, Geometry;
        public int ActorId, Epoch, TraceStep, Support;
        public long InputSequence, AuthorityTick, ObservedTick;
        public ulong MapCommit;
        public double WallTime;
        public float X, Height, Speed;
        public float? Gap, VisualX, VisualHeight, VisualGap;
        public float ReplicaX, ReplicaHeight;
        public bool GroundedAtStart, JumpStarted;
    }
}
#endif
