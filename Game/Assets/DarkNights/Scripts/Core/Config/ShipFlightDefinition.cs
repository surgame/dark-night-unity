using System;

namespace DarkNights.Core.Config
{
    /// <summary>飞行包络与安全着陆的只读规则；LandingTolerance 是接地距离，缓降速度必须低于着陆速度上限。</summary>
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
        public float IdleDescentSpeed { get; }
        public ShipFlightDefinition(float horizontalSpeed = 60, float verticalSpeed = 44, float acceleration = 120,
            float horizontalRange = 128, float maximumLift = 192, double doorSeconds = 1, float landingTolerance = 6, float landingSpeed = 8,
            float idleDescentSpeed = 6)
        {
            double[] values = { horizontalSpeed, verticalSpeed, acceleration, horizontalRange, maximumLift, doorSeconds, landingTolerance, landingSpeed, idleDescentSpeed };
            foreach (double v in values) if (double.IsNaN(v) || v <= 0 || v > 1000) throw new ArgumentOutOfRangeException(nameof(values));
            if (maximumLift > 192 || horizontalRange > 128 || landingTolerance > 8) throw new ArgumentOutOfRangeException(nameof(maximumLift));
            if (idleDescentSpeed > landingSpeed || idleDescentSpeed > verticalSpeed) throw new ArgumentOutOfRangeException(nameof(idleDescentSpeed));
            HorizontalSpeed = horizontalSpeed; VerticalSpeed = verticalSpeed; Acceleration = acceleration;
            HorizontalRange = horizontalRange; MaximumLift = maximumLift; DoorSeconds = doorSeconds;
            LandingTolerance = landingTolerance; LandingSpeed = landingSpeed; IdleDescentSpeed = idleDescentSpeed;
        }
    }
}
