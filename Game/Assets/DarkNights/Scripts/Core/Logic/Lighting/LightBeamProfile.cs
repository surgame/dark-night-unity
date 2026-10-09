using System;

namespace DarkNights.Core.Logic.Lighting
{
    /// <summary>灯口截面和圆弧距离衰减的纯函数；两个照明后端共享形状语义，不拥有光源、装备或地图状态。</summary>
    public static class LightBeamProfile
    {
        public static float Evaluate(float forward, float sideways, float range, float cone,
            float apertureWidth, float feather, bool directional)
        {
            float distance = (float)Math.Sqrt(forward * forward + sideways * sideways);
            if (distance >= range) return 0;
            float beam = 1;
            if (directional)
            {
                if (forward < 0) return 0;
                float side = Math.Max(0, Math.Abs(sideways) - apertureWidth * .5f);
                float length = (float)Math.Sqrt(forward * forward + side * side);
                float cosine = length <= .0001f ? 1 : forward / length;
                float cutoff = (float)Math.Cos(cone * .5f * Math.PI / 180);
                float t = Math.Max(0, Math.Min(1, (cosine - cutoff) / Math.Max(.0001f, feather)));
                beam = t * t * (3 - 2 * t);
            }
            return beam * (float)Math.Pow(Math.Max(0, 1 - distance / range), 1.15);
        }
    }
}
