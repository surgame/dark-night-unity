using DarkNights.Core.Logic.State;
using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>唯一航程状态；任务句柄、客户端 Ready 集合和地图格子不进入池化状态，恢复仅接受稳定阶段。</summary>
    [MemoryPackable, StateData]
    public partial record ExpeditionJourneyState
    {
        public uint Sequence { get; set; }
        public bool Enabled { get; internal set; }
        public string JourneyId { get; internal set; } = "";
        public int Revision { get; internal set; }
        public JourneyPhase Phase { get; internal set; }
        public string PlanetId { get; internal set; } = "";
        public string Seed { get; internal set; } = "";
        public string MapId { get; internal set; } = "";
        public string ContentFingerprint { get; internal set; } = "";
        public double PhaseElapsed { get; internal set; }
        public string Error { get; internal set; } = "";
    }
}
