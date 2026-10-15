using System;

namespace DarkNights.Core.ViewData
{
    /// <summary>单块碎岩的冻结表现轨迹，坐标和重力均使用游戏像素；从静止开始匀加速，水平散落只影响表现，不拥有地形或物理状态。</summary>
    public readonly struct FallingRockViewData
    {
        public readonly float SourceX, SourceY, LandingX, LandingY, Gravity;
        public readonly double Delay;
        public double Duration => Math.Sqrt(2 * (SourceY - LandingY) / Gravity);
        public double Arrival => Delay + Duration;

        public FallingRockViewData(float sourceX, float sourceY, float landingX, float landingY, float gravity, double delay)
        {
            if (!(gravity > 0) || !(sourceY > landingY) || delay < 0 || double.IsNaN(delay))
                throw new ArgumentException("碎岩轨迹必须向下且具有正重力。");
            SourceX = sourceX; SourceY = sourceY; LandingX = landingX; LandingY = landingY;
            Gravity = gravity; Delay = delay;
        }

        public void Sample(double clock, out float x, out float y)
        {
            double time = Math.Max(0, Math.Min(Duration, clock - Delay));
            float progress = (float)(time / Duration);
            x = SourceX + (LandingX - SourceX) * progress * progress;
            y = Math.Max(LandingY, SourceY - .5f * Gravity * (float)(time * time));
        }
    }
}
