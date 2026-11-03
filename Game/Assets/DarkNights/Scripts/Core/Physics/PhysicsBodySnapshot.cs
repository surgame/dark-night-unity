namespace DarkNights.Core.Physics
{
    /// <summary>一次物理切点的不可变体状态；正ID引用角色，负ID引用道具，含预测确认及完整马达临时量而无业务写权限。</summary>
    public sealed class PhysicsBodySnapshot
    {
        public int Id { get; }
        public float X { get; }
        public float Height { get; }
        public float VelocityX { get; }
        public float VelocityY { get; }
        public float Angle { get; }
        public float AngularVelocity { get; }
        public bool Sleeping { get; }
        public bool Grounded { get; }
        public float GroundNormalX { get; }
        public float GroundNormalY { get; }
        public float TakeoffSpeed { get; }
        public float JumpBuffer { get; }
        public bool JumpAscending { get; }
        public long InputSequence { get; }
        public int ControlLease { get; }
        public PhysicsBodySnapshot(int id, float x, float height, float velocityX, float velocityY,
            float angle = 0, float angularVelocity = 0, bool sleeping = false, bool grounded = false,
            float normalX = 0, float normalY = 1, float takeoffSpeed = 0, float jumpBuffer = 0,
            bool ascending = false, long inputSequence = 0, int controlLease = 0)
        {
            if (id == 0 || id < -32 || id > 1000000) throw new System.ArgumentOutOfRangeException(nameof(id));
            PhysicsNumbers.Require(x, -100000, 100000); PhysicsNumbers.Require(height, -100000, 100000);
            PhysicsNumbers.Require(velocityX, -64000, 64000); PhysicsNumbers.Require(velocityY, -64000, 64000);
            PhysicsNumbers.Require(angle, -360, 360); PhysicsNumbers.Require(angularVelocity, -64000, 64000);
            PhysicsNumbers.Require(normalX, -1, 1); PhysicsNumbers.Require(normalY, -1, 1);
            PhysicsNumbers.Require(takeoffSpeed, -64000, 64000); PhysicsNumbers.Require(jumpBuffer, 0, 1);
            if (inputSequence < 0 || controlLease < 0) throw new System.ArgumentOutOfRangeException(nameof(inputSequence));
            Id = id; X = x; Height = height; VelocityX = velocityX; VelocityY = velocityY;
            Angle = angle; AngularVelocity = angularVelocity; Sleeping = sleeping; Grounded = grounded;
            GroundNormalX = normalX; GroundNormalY = normalY; TakeoffSpeed = takeoffSpeed;
            JumpBuffer = jumpBuffer; JumpAscending = ascending; InputSequence = inputSequence; ControlLease = controlLease;
        }
    }
}
