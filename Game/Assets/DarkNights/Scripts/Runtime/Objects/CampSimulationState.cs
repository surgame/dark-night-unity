using DarkNights.Core.Logic.State;
using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 会话对象拥有的模拟时间、实体序号和随机位状态；不是世界实体容器。
    /// 暂停及倍速只在唯一调度入口应用，连接与 Ready 状态不进入此状态。
    /// </summary>
    [MemoryPackable, StateData]
    public partial record CampSimulationState
    {
        public int ExpeditionRun { get; internal set; }
        public int ExpeditionPhase { get; internal set; }
        public double ExpeditionRisk { get; internal set; }
        public double ExpeditionClock { get; internal set; }
        public bool ExpeditionSettled { get; internal set; }
        public int RobotModule { get; internal set; }
        public int CargoModule { get; internal set; }
        public int CrewModule { get; internal set; }
        public int LostCargo { get; internal set; }
        public int LostDevices { get; internal set; }
        public int ResupplyCost { get; internal set; }
        public uint Sequence { get; set; }
        public SessionMode Mode { get; internal set; }
        public bool Paused { get; internal set; }
        public int Speed { get; internal set; }
        public double Elapsed { get; internal set; }
        public int NextEntityId { get; internal set; }
        public ulong RandomSeed { get; internal set; }
        public ulong RandomState { get; internal set; }
        public int Kills { get; internal set; }
        public int Lost { get; internal set; }
    }
}
