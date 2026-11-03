using System;

namespace DarkNights.Core.Physics
{
    /// <summary>稳定端点之间的单根牵引约束；拥有者只表示角色关系，控制权限仍由会话当前连接和租约校验。</summary>
    public sealed class PhysicsRopeSnapshot
    {
        public int OwnerId { get; }
        public int TargetId { get; }
        public float Length { get; }
        public int HangingRopeId { get; }
        public float Position { get; }
        public PhysicsRopeSnapshot(int ownerId, int targetId, float length, int hangingRopeId = 0, float position = 0)
        {
            if (ownerId <= 0 || targetId == 0 || targetId == ownerId) throw new ArgumentException("Invalid rope endpoints.");
            if (hangingRopeId < 0 || hangingRopeId > 8 || hangingRopeId != 0 && targetId >= 0)
                throw new ArgumentException("Invalid hanging rope identity.");
            PhysicsNumbers.Require(length, hangingRopeId == 0 ? 4 : 0, 512);
            PhysicsNumbers.Require(position, 0, 1536);
            if (hangingRopeId == 0 && position != 0 || hangingRopeId != 0 && length != 0)
                throw new ArgumentException("Rope mode fields are inconsistent.");
            OwnerId = ownerId; TargetId = targetId; Length = length;
            HangingRopeId = hangingRopeId; Position = position;
        }
    }
}
