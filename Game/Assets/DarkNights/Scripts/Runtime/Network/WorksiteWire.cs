using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// WorksiteViewData 的 MemoryPack 具体 wire 类型，仅负责传输字段；发送后不修改，接收立即冻结后交给展示层。
    /// 可变实例不属于客户端世界，不能跨状态池回调保留；字段顺序变更必须升级握手协议。
    /// </summary>
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
        {
            Id = value.Id,
            Kind = value.Kind,
            X = value.X,
            WorkerId = value.WorkerId,
            Amount = value.Amount,
            Progress = value.Progress,
            Variant = value.Variant,
            FarmId = value.FarmId,
        };

        public WorksiteViewData Freeze() => new WorksiteViewData(
            Id,
            Kind,
            X,
            WorkerId,
            Amount,
            Progress,
            Variant,
            FarmId);
    }
}
