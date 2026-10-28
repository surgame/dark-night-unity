using System;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 单位移动配置在激活时冻结的只读值，统一目标到达、主动跳跃与空中操控开关。
    /// 不持有速度或输入；基础步速、跳速与重力仍沿用既有balance规则，避免第二份数值源。
    /// </summary>
    public sealed class MovementDefinition
    {
        public const float DefaultSpeedRatio = .35f;
        public const float DefaultAccelerationSeconds = .28f;
        public const float DefaultBrakingSeconds = .55f;
        public const float DefaultDragPerSecond = .08f;

        public float ArrivalDistance { get; }
        public bool CanJump { get; }
        public bool EnableAirControl { get; }
        public float AirSpeedRatio { get; }
        public float AirAccelerationSeconds { get; }
        public float AirBrakingSeconds { get; }
        public float AirDragPerSecond { get; }

        public MovementDefinition(float arrivalDistance, bool canJump, bool enableAirControl,
            float speedRatio, float accelerationSeconds, float brakingSeconds, float dragPerSecond)
        {
            Validate(arrivalDistance, float.Epsilon, 4);
            Validate(speedRatio, 0, 1);
            Validate(accelerationSeconds, .01f, 5);
            Validate(brakingSeconds, .01f, 5);
            Validate(dragPerSecond, 0, 5);
            ArrivalDistance = arrivalDistance;
            CanJump = canJump;
            EnableAirControl = enableAirControl;
            AirSpeedRatio = speedRatio;
            AirAccelerationSeconds = accelerationSeconds;
            AirBrakingSeconds = brakingSeconds;
            AirDragPerSecond = dragPerSecond;
        }

        private static void Validate(float value, float minimum, float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentException("Movement parameter is outside its simulation bounds.");
        }
    }
}
