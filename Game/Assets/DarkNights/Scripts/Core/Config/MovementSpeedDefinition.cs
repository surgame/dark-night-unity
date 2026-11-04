using System;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 单个移动速度预设的冻结数值，统一手动角色步速、冲刺上限及两档速度之间的过渡时间。
    /// 配置随所属MovementDefinition共用，不持有输入、速度或独立时钟；距离单位为逻辑像素。
    /// </summary>
    public sealed class MovementSpeedDefinition
    {
        public float WalkSpeed { get; }
        public float SprintSpeed { get; }
        public float SprintAccelerationSeconds { get; }
        public float SprintDecelerationSeconds { get; }

        public MovementSpeedDefinition(float walkSpeed, float sprintSpeed,
            float accelerationSeconds, float decelerationSeconds)
        {
            if (!Finite(walkSpeed) || walkSpeed <= 0 || walkSpeed > 1000 ||
                !Finite(sprintSpeed) || sprintSpeed < walkSpeed || sprintSpeed > walkSpeed * 4 ||
                !Finite(accelerationSeconds) || accelerationSeconds < .01f || accelerationSeconds > 5 ||
                !Finite(decelerationSeconds) || decelerationSeconds < .01f || decelerationSeconds > 5)
                throw new ArgumentException("移动速度预设超出允许范围。");
            WalkSpeed = walkSpeed;
            SprintSpeed = sprintSpeed;
            SprintAccelerationSeconds = accelerationSeconds;
            SprintDecelerationSeconds = decelerationSeconds;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
