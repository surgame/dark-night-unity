namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 工位的冻结展示值，供剩余资源、生产进度和占用查询；农田关联及变体沿用权威身份。
    /// </summary>
    public sealed class WorksiteViewData
    {
        public int Id { get; }
        public string Kind { get; }
        public float X { get; }
        public int WorkerId { get; }
        public int Amount { get; }
        public double Progress { get; }
        public int Variant { get; }
        public int FarmId { get; }

        public WorksiteViewData(
            int id,
            string kind,
            float x,
            int workerId,
            int amount,
            double progress,
            int variant,
            int farmId)
        {
            Id = id;
            Kind = kind;
            X = x;
            WorkerId = workerId;
            Amount = amount;
            Progress = progress;
            Variant = variant;
            FarmId = farmId;
        }
    }
}
