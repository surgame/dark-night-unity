using System;

namespace DarkNights.Core.Physics
{
    /// <summary>作者物理道具的冻结初值；负ID与角色实体ID隔离，长度使用逻辑像素，实例进度不写回定义。</summary>
    public sealed class PhysicsPropDefinition
    {
        public int Id { get; }
        public float X { get; }
        public float Height { get; }
        public float Width { get; }
        public float BodyHeight { get; }
        public float Mass { get; }
        public PhysicsPropDefinition(int id, float x, float height, float width, float bodyHeight, float mass)
        {
            if (id >= 0 || id < -32) throw new ArgumentOutOfRangeException(nameof(id));
            PhysicsNumbers.Require(x, -100000, 100000);
            PhysicsNumbers.Require(height, -100000, 100000);
            PhysicsNumbers.Require(width, 2, 256);
            PhysicsNumbers.Require(bodyHeight, 2, 256);
            PhysicsNumbers.Require(mass, .1f, 100);
            Id = id; X = x; Height = height; Width = width; BodyHeight = bodyHeight; Mass = mass;
        }
    }
}
