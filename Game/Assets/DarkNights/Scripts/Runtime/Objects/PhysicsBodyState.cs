using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>物理道具唯一状态的按值记录；只用于PhysicsWorldState的深复制数组，不重复保存角色状态。</summary>
    [MemoryPackable]
    public partial struct PhysicsBodyState
    {
        public int Id;
        public float X, Height, VelocityX, VelocityY, Angle, AngularVelocity;
        public bool Sleeping;
    }
}
