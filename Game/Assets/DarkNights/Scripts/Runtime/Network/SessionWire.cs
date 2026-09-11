using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// SessionViewData 的 MemoryPack 具体 wire 类型，仅负责传输字段；发送后不修改，接收立即冻结后交给展示层。
    /// 可变实例不属于客户端世界，不能跨状态池回调保留；字段顺序变更必须升级握手协议。
    /// </summary>
    [MemoryPackable]
    public partial class SessionWire
    {
        public long Publication { get; set; }
        public int Epoch { get; set; }
        public int Revision { get; set; }
        public long ServerTick { get; set; }
        public int PolicyRevision { get; set; }
        public bool HostOnly { get; set; }
        public int PlayerCount { get; set; }
        public int ReadyCount { get; set; }
        public bool Loading { get; set; }
        public bool Paused { get; set; }
        public int Speed { get; set; }
        public double Elapsed { get; set; }
        public WorldWire World { get; set; }
        public PresentationWire[] Events { get; set; }
        public PresentationWire[] Remnants { get; set; }

        public static SessionWire From(SessionViewData value) => new SessionWire
        {
            Publication = value.Publication,
            Epoch = value.Epoch,
            Revision = value.Revision,
            ServerTick = value.ServerTick,
            PolicyRevision = value.PolicyRevision,
            HostOnly = value.HostOnly,
            PlayerCount = value.PlayerCount,
            ReadyCount = value.ReadyCount,
            Loading = value.Loading,
            Paused = value.Paused,
            Speed = value.Speed,
            Elapsed = value.Elapsed,
            World = WorldWire.From(value.World),
            Events = value.Events.Select(PresentationWire.From).ToArray(),
            Remnants = value.Remnants.Select(PresentationWire.From).ToArray(),
        };

        public SessionViewData Freeze() => new SessionViewData(
            Publication,
            Epoch,
            Revision,
            ServerTick,
            PolicyRevision,
            HostOnly,
            PlayerCount,
            ReadyCount,
            Loading,
            Paused,
            Speed,
            Elapsed,
            World?.Freeze(), Events?.Select(item => item.Freeze()).ToArray(), Remnants?.Select(item => item.Freeze()).ToArray());
    }
}
