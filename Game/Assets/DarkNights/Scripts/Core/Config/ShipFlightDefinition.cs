using System;

namespace DarkNights.Core.Config
{
    /// <summary>首版泊位试飞只读规则；作者值取 balance.expedition.Ship，包络和像素几何另由 ShipGeometry 固定。</summary>
    public sealed class ShipFlightDefinition
    {
        public float HorizontalSpeed { get; }
        public float VerticalSpeed { get; }
        public float Acceleration { get; }
        public float HorizontalRange { get; }
        public float MaximumLift { get; }
        public double DoorSeconds { get; }
        public float LandingTolerance { get; }
        public float LandingSpeed { get; }
        public ShipFlightDefinition(float horizontalSpeed = 60, float verticalSpeed = 44, float acceleration = 120,
            float horizontalRange = 128, float maximumLift = 192, double doorSeconds = 1, float landingTolerance = 6, float landingSpeed = 8)
        {
            double[] values = { horizontalSpeed, verticalSpeed, acceleration, horizontalRange, maximumLift, doorSeconds, landingTolerance, landingSpeed };
            foreach (double v in values) if (double.IsNaN(v) || v <= 0 || v > 1000) throw new ArgumentOutOfRangeException(nameof(values));
            if (maximumLift > 192 || horizontalRange > 128 || landingTolerance > 8) throw new ArgumentOutOfRangeException(nameof(maximumLift));
            HorizontalSpeed = horizontalSpeed; VerticalSpeed = verticalSpeed; Acceleration = acceleration;
            HorizontalRange = horizontalRange; MaximumLift = maximumLift; DoorSeconds = doorSeconds;
            LandingTolerance = landingTolerance; LandingSpeed = landingSpeed;
        }
    }
}
