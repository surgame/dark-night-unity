using DarkNights.Core.Logic.State;
using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 会话级可靠状态投影，供客户端显示 epoch、修订、控制策略、Ready 与时间控制结果。
    /// 它不拥有经济、实体或模拟状态；实例由 YYGC 状态池管理，订阅者不得跨回调保留可变引用。
    /// </summary>
    [MemoryPackable, StateData]
    public partial record SessionStatusState
    {
        public uint Sequence { get; set; }
        public int Protocol { get; set; }
        public int Epoch { get; set; }
        public int Revision { get; set; }
        public int PolicyRevision { get; set; }
        public CampControlMode ControlMode { get; set; }
        public int ReadyCount { get; set; }
        public int PlayerCount { get; set; }
        public bool Paused { get; set; }
        public int SpeedMultiplier { get; set; }
        public byte[] ProjectionPayload { get; set; }
    }
}
