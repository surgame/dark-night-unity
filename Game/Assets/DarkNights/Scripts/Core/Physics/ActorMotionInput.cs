using System;

namespace DarkNights.Core.Physics
{
    /// <summary>
    /// 一个角色在当前模拟步消费的冻结运动意图，不持有引擎对象、可写状态或设备输入。
    /// 长度和速度沿用逻辑像素及逻辑像素每秒，DeltaSeconds为已应用会话倍速的秒数。
    /// 角色身份由既有会话校验，本合同只约束本步输入，不充当网络授权或物理恢复检查点。
    /// </summary>
    public readonly struct ActorMotionInput
    {
        public int ActorId { get; }
        public double DeltaSeconds { get; }
        public float Direction { get; }
        public int Climb { get; }
        public bool JumpPressed { get; }
        public bool DropPressed { get; }
        public bool JumpHeld { get; }
        public bool SprintHeld { get; }
        public float? GoalX { get; }
        public float? FlightHeight { get; }
        public float? NormalSpeed { get; }

        public ActorMotionInput(int actorId, double deltaSeconds, float direction,
            bool jumpPressed, bool dropPressed, bool jumpHeld, bool sprintHeld,
            float? goalX, float? flightHeight, float? normalSpeed, int climb = 0)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (float.IsNaN(direction) || direction < -1 || direction > 1)
                throw new ArgumentOutOfRangeException(nameof(direction));
            Validate(goalX, nameof(goalX));
            Validate(flightHeight, nameof(flightHeight));
            Validate(normalSpeed, nameof(normalSpeed));
            if (normalSpeed < 0) throw new ArgumentOutOfRangeException(nameof(normalSpeed));
            ActorId = actorId;
            DeltaSeconds = deltaSeconds;
            if (climb < -1 || climb > 1) throw new ArgumentOutOfRangeException(nameof(climb));
            Direction = direction; Climb = climb;
            JumpPressed = jumpPressed;
            DropPressed = dropPressed;
            JumpHeld = jumpHeld;
            SprintHeld = sprintHeld;
            GoalX = goalX;
            FlightHeight = flightHeight;
            NormalSpeed = normalSpeed;
        }

        private static void Validate(float? value, string name)
        {
            if (value.HasValue && (float.IsNaN(value.Value) || float.IsInfinity(value.Value)))
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
