namespace DarkNights.Core.Save
{
    /// <summary>普通工位的恢复记录；保留存量、进度与占用，矿床格状态由独立合同保存。</summary>
    public sealed class WorksiteSnapshot
    {
        public int Id { get; }
        public string Kind { get; }
        public double X { get; }
        public int WorkerId { get; }
        public int Amount { get; }
        public double Progress { get; }
        public int Variant { get; }
        public int FarmId { get; }
        public WorksiteSnapshot(int id, string kind, double x, int workerId, int amount, double progress, int variant, int farmId)
        { Id = id; Kind = kind; X = x; WorkerId = workerId; Amount = amount; Progress = progress; Variant = variant; FarmId = farmId; }
    }
}
