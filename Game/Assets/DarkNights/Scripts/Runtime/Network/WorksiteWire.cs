using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>普通工位的会话 wire 合同；矿床改用独立 DTO，字段随协议升版且收包立即冻结。</summary>
    [MemoryPackable]
    public partial class WorksiteWire
    {
        public int Id { get; set; }
        public string Kind { get; set; }
        public float X { get; set; }
        public int WorkerId { get; set; }
        public int Amount { get; set; }
        public double Progress { get; set; }
        public int Variant { get; set; }
        public int FarmId { get; set; }
        public static WorksiteWire From(WorksiteViewData value) => new WorksiteWire
        { Id = value.Id, Kind = value.Kind, X = value.X, WorkerId = value.WorkerId, Amount = value.Amount,
            Progress = value.Progress, Variant = value.Variant, FarmId = value.FarmId };
        public WorksiteViewData Freeze() => new WorksiteViewData(Id, Kind, X, WorkerId, Amount, Progress, Variant, FarmId);
    }
}
