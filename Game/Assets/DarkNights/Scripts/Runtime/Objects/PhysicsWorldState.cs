using System;
using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>物理能力独占的道具与约束状态；角色姿态仍归ActorState，原生求解器仅为会话执行资源。</summary>
    [MemoryPackable, StateData]
    public partial record PhysicsWorldState
    {
        public uint Sequence { get; set; }
        public long Tick { get; internal set; }
        public ulong CollisionRevision { get; internal set; }
        public PhysicsBodyState[] Props { get; internal set; } = Array.Empty<PhysicsBodyState>();
        public PhysicsRopeState[] Ropes { get; internal set; } = Array.Empty<PhysicsRopeState>();
        public void CopyFrom(IStateData source)
        {
            if (!(source is PhysicsWorldState value)) throw new ArgumentException("Expected physics world state.");
            Sequence = value.Sequence; Tick = value.Tick; CollisionRevision = value.CollisionRevision;
            Props = (PhysicsBodyState[])value.Props.Clone(); Ropes = (PhysicsRopeState[])value.Ropes.Clone();
        }
    }
}
