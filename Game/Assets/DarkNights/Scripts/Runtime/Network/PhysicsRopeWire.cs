using DarkNights.Core.Physics;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>稳定角色／道具身份构成的绳索传输合同；冻结时校验长度，整体帧再校验端点闭包。</summary>
    [MemoryPackable]
    public partial class PhysicsRopeWire
    {
        public int OwnerId { get; set; }
        public int TargetId { get; set; }
        public float Length { get; set; }
        public int HangingRopeId { get; set; }
        public float Position { get; set; }
        public static PhysicsRopeWire From(PhysicsRopeSnapshot r) => new PhysicsRopeWire
            { OwnerId = r.OwnerId, TargetId = r.TargetId, Length = r.Length, HangingRopeId = r.HangingRopeId, Position = r.Position };
        public PhysicsRopeSnapshot Freeze() => new PhysicsRopeSnapshot(OwnerId, TargetId, Length, HangingRopeId, Position);
    }
}
