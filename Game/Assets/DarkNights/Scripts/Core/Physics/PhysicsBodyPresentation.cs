using System;

namespace DarkNights.Core.Physics
{
    /// <summary>
    /// 物理根之外的只读显示采样；同一切点的角色、道具和绳端复用相同比例。
    /// 身份、世界或明显传送不连续时直接采用当前姿态，不反写物理或业务状态。
    /// </summary>
    public readonly struct PhysicsBodyPresentation
    {
        public float X { get; }
        public float Height { get; }
        public float Angle { get; }
        public PhysicsBodyPresentation(PhysicsBodySnapshot previous, PhysicsBodySnapshot current, double ratio)
        {
            float t = (float)Math.Clamp(ratio, 0, 1);
            if (previous == null || previous.Id != current.Id || previous.ControlLease != current.ControlLease ||
                Math.Abs(previous.X - current.X) > 32 || Math.Abs(previous.Height - current.Height) > 32)
                t = 1;
            X = t >= 1 ? current.X : previous.X + (current.X - previous.X) * t;
            Height = t >= 1 ? current.Height : previous.Height + (current.Height - previous.Height) * t;
            float angle = previous == null ? 0 : ((current.Angle - previous.Angle + 540) % 360) - 180;
            Angle = t >= 1 ? current.Angle : previous.Angle + angle * t;
        }
    }
}
