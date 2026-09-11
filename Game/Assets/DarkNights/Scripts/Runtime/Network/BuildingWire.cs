using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// BuildingViewData 的 MemoryPack 具体 wire 类型，仅负责传输字段；发送后不修改，接收立即冻结后交给展示层。
    /// 可变实例不属于客户端世界，不能跨状态池回调保留；字段顺序变更必须升级握手协议。
    /// </summary>
    [MemoryPackable]
    public partial class BuildingWire
    {
        public int Id { get; set; }
        public string Kind { get; set; }
        public float X { get; set; }
        public double Hp { get; set; }
        public double Progress { get; set; }
        public int WorkerId { get; set; }
        public int FarmSiteId { get; set; }
        public double HitFlash { get; set; }
        public TrainingWire[] Training { get; set; }

        public static BuildingWire From(BuildingViewData value) => new BuildingWire
        {
            Id = value.Id,
            Kind = value.Kind,
            X = value.X,
            Hp = value.Hp,
            Progress = value.Progress,
            WorkerId = value.WorkerId,
            FarmSiteId = value.FarmSiteId,
            HitFlash = value.HitFlash,
            Training = value.Training.Select(TrainingWire.From).ToArray(),
        };

        public BuildingViewData Freeze() => new BuildingViewData(
            Id,
            Kind,
            X,
            Hp,
            Progress,
            WorkerId,
            FarmSiteId,
            HitFlash,
            Training?.Select(item => item?.Freeze()).ToArray());
    }
}
