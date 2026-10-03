using System;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 主角运动数值从 balance.json 的 hero_control 读取；原生人物尺寸和步幅合同由本类集中声明。
    /// 手动主角拥有独立横向速度，NPC 仍读取职业速度；所有距离使用逻辑像素坐标。
    /// </summary>
    public sealed class HeroControlDefinition
    {
        // 原生主角 12px 高，以整数四倍显示为 48 逻辑像素（3 格）；碰撞略收进身体轮廓。
        public const float VisualScale = 4, BodyHalfWidth = 8, BodyHeight = 44, WalkCycleDistance = 64;
        public float WalkSpeed { get; }
        public float JumpSpeed { get; }
        public float Gravity { get; }
        public float MaximumHeight { get; }
        public float JetpackSpeed { get; }
        public double FuelSeconds { get; }
        public double FuelRecovery { get; }
        public double DropSeconds { get; }
        public float WorkReach { get; }
        public float SprintMultiplier { get; }
        public HeroJumpStrategy JumpStrategy { get; }

        public HeroControlDefinition(double jumpSpeed, double gravity, double maximumHeight,
            double jetpackSpeed, double fuelSeconds, double fuelRecovery, double dropSeconds, double workReach,
            double sprintMultiplier = 1.8, double walkSpeed = 112, HeroJumpStrategy jumpStrategy = HeroJumpStrategy.Fixed)
        {
            foreach (double value in new[] { jumpSpeed, gravity, maximumHeight, jetpackSpeed,
                fuelSeconds, fuelRecovery, dropSeconds, workReach, walkSpeed })
                if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0 || value > 1000)
                    throw new ArgumentException("Invalid hero control configuration.");
            JumpSpeed = (float)jumpSpeed; Gravity = (float)gravity; MaximumHeight = (float)maximumHeight;
            JetpackSpeed = (float)jetpackSpeed; FuelSeconds = fuelSeconds; FuelRecovery = fuelRecovery;
            DropSeconds = dropSeconds; WorkReach = (float)workReach;
            if (double.IsNaN(sprintMultiplier) || double.IsInfinity(sprintMultiplier) || sprintMultiplier < 1 || sprintMultiplier > 4)
                throw new ArgumentException("Invalid sprint multiplier.");
            SprintMultiplier = (float)sprintMultiplier;
            WalkSpeed = (float)walkSpeed;
            if (!Enum.IsDefined(typeof(HeroJumpStrategy), jumpStrategy)) throw new ArgumentException("Invalid jump strategy.");
            JumpStrategy = jumpStrategy;
        }

        /// <summary>正式地面、船舱和工作台共用的步速；调用方负责可信输入和调试倍率。</summary>
        public float MoveSpeed(bool sprint) => WalkSpeed * (sprint ? SprintMultiplier : 1);
    }
}
