using System;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 相邻权威模拟步之间的只读角色显示采样，仅插值坐标和连续动作时间，不外推或结算运动。
    /// 调用方保证两帧来自同一世界的相邻步；换身份、控制租约、暂停或传送时丢弃旧帧。
    /// </summary>
    public readonly struct ActorPresentationSample
    {
        public float X { get; }
        public float Height { get; }
        public double ActionTime { get; }

        private ActorPresentationSample(float x, float height, double actionTime)
        {
            X = x;
            Height = height;
            ActionTime = actionTime;
        }

        public static ActorPresentationSample Interpolate(ActorViewData previous, ActorViewData current, double fraction)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (previous == null || previous.Id != current.Id || previous.Kind != current.Kind ||
                previous.ManualControl != current.ManualControl || previous.ControllerSlot != current.ControllerSlot ||
                previous.ControlLease != current.ControlLease || Math.Abs(current.X - previous.X) >= 32 ||
                Math.Abs(current.Height - previous.Height) >= 32)
                return new ActorPresentationSample(current.X, current.Height, current.ActionTime);

            double ratio = double.IsNaN(fraction) ? 1 : Math.Max(0, Math.Min(1, fraction));
            double time = previous.Activity == current.Activity && current.ActionTime >= previous.ActionTime
                ? previous.ActionTime + (current.ActionTime - previous.ActionTime) * ratio : current.ActionTime;
            return new ActorPresentationSample(previous.X + (current.X - previous.X) * (float)ratio,
                previous.Height + (current.Height - previous.Height) * (float)ratio, time);
        }
    }
}
