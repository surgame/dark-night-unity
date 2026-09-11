using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// TrainingViewData 的 MemoryPack 具体 wire 类型，仅负责传输字段；发送后不修改，接收立即冻结后交给展示层。
    /// 可变实例不属于客户端世界，不能跨状态池回调保留；字段顺序变更必须升级握手协议。
    /// </summary>
    [MemoryPackable]
    public partial class TrainingWire
    {
        public int ActorId { get; set; }
        public string Kind { get; set; }
        public double Remaining { get; set; }

        public static TrainingWire From(TrainingViewData value) => new TrainingWire
        {
            ActorId = value.ActorId,
            Kind = value.Kind,
            Remaining = value.Remaining,
        };

        public TrainingViewData Freeze() => new TrainingViewData(
            ActorId,
            Kind,
            Remaining);
    }
}
