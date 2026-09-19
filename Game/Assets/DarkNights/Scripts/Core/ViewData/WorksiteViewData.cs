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
        public float Y { get; }
        public int WorkerId { get; }
        public int Amount { get; }
        public double Progress { get; }
        public int Variant { get; }
        public int FarmId { get; }
        public bool IsMineralDeposit { get; }
        public string RoomKind { get; }
        public string Rarity { get; }
        public int Capacity { get; }
        public string Stage { get; }

        public WorksiteViewData(
            int id,
            string kind,
            float x,
            int workerId,
            int amount,
            double progress,
            int variant,
            int farmId)
            : this(id, kind, x, 0, workerId, amount, progress, variant, farmId, false, "", "", 0, "")
        {
        }

        public WorksiteViewData(
            int id,
            string kind,
            float x,
            float y,
            int workerId,
            int amount,
            double progress,
            int variant,
            int farmId,
            bool isMineralDeposit,
            string roomKind,
            string rarity,
            int capacity,
            string stage)
        {
            Id = id;
            Kind = kind;
            X = x;
            Y = y;
            WorkerId = workerId;
            Amount = amount;
            Progress = progress;
            Variant = variant;
            FarmId = farmId;
            IsMineralDeposit = isMineralDeposit;
            RoomKind = roomKind ?? "";
            Rarity = rarity ?? "";
            Capacity = capacity;
            Stage = stage ?? "";
        }

        public WorksiteViewData(
            int id,
            string kind,
            float x,
            int workerId,
            int amount,
            double progress,
            int variant,
            int farmId,
            bool isMineralDeposit,
            string roomKind,
            string rarity,
            int capacity,
            string stage)
            : this(id, kind, x, 0, workerId, amount, progress, variant, farmId, isMineralDeposit,
                roomKind, rarity, capacity, stage)
        {
        }
    }
}
