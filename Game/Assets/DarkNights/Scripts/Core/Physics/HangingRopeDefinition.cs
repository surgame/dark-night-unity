using System;
using System.Collections.Generic;

namespace DarkNights.Core.Physics
{
    /// <summary>
    /// 固定环境吊绳的冻结拓扑与运动规则；锚点及长度使用逻辑像素，质量为kg。
    /// 绳节复用物理会话的负ID刚体，定义只生成初值，不拥有运行姿态或人物抓附状态。
    /// </summary>
    public sealed class HangingRopeDefinition
    {
        public const float GripSpacing = 52;
        public int Id { get; }
        public int FirstBodyId { get; }
        public int Count { get; }
        public float AnchorX { get; }
        public float AnchorHeight { get; }
        public float SegmentLength { get; }
        public float Mass { get; }
        public float Reach { get; }
        public float ClimbSpeed { get; }
        public float SwingAcceleration { get; }
        public float Length => Count * SegmentLength;

        public HangingRopeDefinition(int id, int firstBodyId, int count, float x, float height,
            float segmentLength, float mass, float reach, float climbSpeed, float swingAcceleration)
        {
            if (id < 1 || id > 8 || firstBodyId >= 0 || count < 2 || count > 12 ||
                firstBodyId - count + 1 < -32) throw new ArgumentException("吊绳身份或绳节范围无效。");
            PhysicsNumbers.Require(x, -100000, 100000);
            PhysicsNumbers.Require(height, -100000, 100000);
            PhysicsNumbers.Require(segmentLength, 16, 128);
            PhysicsNumbers.Require(mass, .1f, 10);
            PhysicsNumbers.Require(reach, 8, 64);
            PhysicsNumbers.Require(climbSpeed, 1, 160);
            PhysicsNumbers.Require(swingAcceleration, 1, 1600);
            Id = id; FirstBodyId = firstBodyId; Count = count; AnchorX = x; AnchorHeight = height;
            SegmentLength = segmentLength; Mass = mass; Reach = reach;
            ClimbSpeed = climbSpeed; SwingAcceleration = swingAcceleration;
        }

        public bool Contains(int bodyId) => bodyId <= FirstBodyId && bodyId > FirstBodyId - Count;

        public IEnumerable<PhysicsPropDefinition> InitialBodies()
        {
            for (int i = 0; i < Count; i++)
                yield return new PhysicsPropDefinition(FirstBodyId - i, AnchorX,
                    AnchorHeight - (i + 1) * SegmentLength, 6, SegmentLength, Mass);
        }
    }
}
