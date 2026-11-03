using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>物理会话拥有的牵引端点和长度；值数组复制隔离事务与状态池，不持有原生Joint引用。</summary>
    [MemoryPackable]
    public partial struct PhysicsRopeState
    {
        public int OwnerId, TargetId;
        public float Length;
        public int HangingRopeId;
        public float Position;
    }
}
