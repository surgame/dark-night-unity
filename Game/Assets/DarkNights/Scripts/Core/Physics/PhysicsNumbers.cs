using System;

namespace DarkNights.Core.Physics
{
    /// <summary>当前浮点物理合同的单位及有限范围检查；只约束边界，不模拟或隐式量化权威数值。</summary>
    public static class PhysicsNumbers
    {
        public const float PixelsPerUnit = 100;
        public const int MaximumBodies = 64;
        public const int MaximumRopes = 8;
        public static void Require(float value, float minimum, float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentOutOfRangeException(nameof(value), "Physics value exceeds its finite contract.");
        }
    }
}
