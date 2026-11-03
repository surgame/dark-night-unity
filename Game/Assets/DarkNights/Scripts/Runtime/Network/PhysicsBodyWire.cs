using DarkNights.Core.Physics;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 浮点物理体的有界传输适配；字段顺序由协议固定，接收立即验证并冻结。
    /// 不传原生组件、接触指针或引擎缓存，也不授予客户端状态写权限。
    /// </summary>
    [MemoryPackable]
    public partial class PhysicsBodyWire
    {
        public int Id { get; set; }
        public float X { get; set; }
        public float Height { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public float Angle { get; set; }
        public float AngularVelocity { get; set; }
        public bool Sleeping { get; set; }
        public bool Grounded { get; set; }
        public float NormalX { get; set; }
        public float NormalY { get; set; }
        public float TakeoffSpeed { get; set; }
        public float JumpBuffer { get; set; }
        public bool JumpAscending { get; set; }
        public long InputSequence { get; set; }
        public int ControlLease { get; set; }

        public static PhysicsBodyWire From(PhysicsBodySnapshot b) => new PhysicsBodyWire
        {
            Id = b.Id, X = b.X, Height = b.Height, VelocityX = b.VelocityX, VelocityY = b.VelocityY,
            Angle = b.Angle, AngularVelocity = b.AngularVelocity, Sleeping = b.Sleeping, Grounded = b.Grounded,
            NormalX = b.GroundNormalX, NormalY = b.GroundNormalY, TakeoffSpeed = b.TakeoffSpeed,
            JumpBuffer = b.JumpBuffer, JumpAscending = b.JumpAscending,
            InputSequence = b.InputSequence, ControlLease = b.ControlLease
        };

        public PhysicsBodySnapshot Freeze() => new PhysicsBodySnapshot(Id, X, Height, VelocityX, VelocityY, Angle,
            AngularVelocity, Sleeping, Grounded, NormalX, NormalY, TakeoffSpeed, JumpBuffer, JumpAscending, InputSequence, ControlLease);
    }
}
