using System;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 主角新增运动能力的只读数值，唯一来源为 balance.json 的 hero_control。
    /// 单位横向速度、伤害和攻击时序仍来自原职业规则；所有距离使用原玩法像素坐标。
    /// </summary>
    public sealed class HeroControlDefinition
    {
        public float JumpSpeed { get; }
        public float Gravity { get; }
        public float MaximumHeight { get; }
        public float JetpackSpeed { get; }
        public double FuelSeconds { get; }
        public double FuelRecovery { get; }
        public double DropSeconds { get; }
        public float WorkReach { get; }

        public HeroControlDefinition(double jumpSpeed, double gravity, double maximumHeight,
            double jetpackSpeed, double fuelSeconds, double fuelRecovery, double dropSeconds, double workReach)
        {
            foreach (double value in new[] { jumpSpeed, gravity, maximumHeight, jetpackSpeed,
                fuelSeconds, fuelRecovery, dropSeconds, workReach })
                if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0 || value > 1000)
                    throw new ArgumentException("Invalid hero control configuration.");
            JumpSpeed = (float)jumpSpeed; Gravity = (float)gravity; MaximumHeight = (float)maximumHeight;
            JetpackSpeed = (float)jetpackSpeed; FuelSeconds = fuelSeconds; FuelRecovery = fuelRecovery;
            DropSeconds = dropSeconds; WorkReach = (float)workReach;
        }
    }
}
