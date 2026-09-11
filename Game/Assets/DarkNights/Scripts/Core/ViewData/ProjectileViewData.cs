namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 在飞箭矢的冻结轨迹。ViewId 只在当前 epoch 内稳定，供视图跟踪；无伤害和目标结算能力，不复用列表下标作为身份。
    /// </summary>
    public sealed class ProjectileViewData
    {
        public long ViewId { get; }
        public float FromX { get; }
        public float FromY { get; }
        public float ToX { get; }
        public float ToY { get; }
        public double Age { get; }
        public double Duration { get; }

        public ProjectileViewData(
            long viewId,
            float fromX,
            float fromY,
            float toX,
            float toY,
            double age,
            double duration)
        {
            ViewId = viewId;
            FromX = fromX;
            FromY = fromY;
            ToX = toX;
            ToY = toY;
            Age = age;
            Duration = duration;
        }
    }
}
