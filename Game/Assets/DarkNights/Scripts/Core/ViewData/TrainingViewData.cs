namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 兵营队列的冻结展示项，保留原先顺序与剩余时间；副本不推进训练、不支付或转职。
    /// </summary>
    public sealed class TrainingViewData
    {
        public int ActorId { get; }
        public string Kind { get; }
        public double Remaining { get; }

        public TrainingViewData(
            int actorId,
            string kind,
            double remaining)
        {
            ActorId = actorId;
            Kind = kind;
            Remaining = remaining;
        }
    }
}
