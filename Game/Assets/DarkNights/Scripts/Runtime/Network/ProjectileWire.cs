using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// ProjectileViewData 的 MemoryPack 具体 wire 类型，仅负责传输字段；发送后不修改，接收立即冻结后交给展示层。
    /// 可变实例不属于客户端世界，不能跨状态池回调保留；字段顺序变更必须升级握手协议。
    /// </summary>
    [MemoryPackable]
    public partial class ProjectileWire
    {
        public long ViewId { get; set; }
        public float FromX { get; set; }
        public float FromY { get; set; }
        public float ToX { get; set; }
        public float ToY { get; set; }
        public double Age { get; set; }
        public double Duration { get; set; }

        public static ProjectileWire From(ProjectileViewData value) => new ProjectileWire
        {
            ViewId = value.ViewId,
            FromX = value.FromX,
            FromY = value.FromY,
            ToX = value.ToX,
            ToY = value.ToY,
            Age = value.Age,
            Duration = value.Duration,
        };

        public ProjectileViewData Freeze() => new ProjectileViewData(
            ViewId,
            FromX,
            FromY,
            ToX,
            ToY,
            Age,
            Duration);
    }
}
