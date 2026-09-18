using GameCore.Objects.NetworkStates;
using DarkNights.Core.Config;
using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>单个矿床的唯一容量、稀有度和钻机状态；地图只保存其静态生成标记，不能复制这份运行进度。</summary>
    [MemoryPackable, StateData]
        public partial record MineralDepositState
    {
        public uint Sequence { get; set; }
        public int Id { get; internal set; }
        public string PlacementKey { get; internal set; }
        public float X { get; internal set; }
        public int Y { get; internal set; }
        public string RoomKind { get; internal set; }
        public string Rarity { get; internal set; }
        public int Capacity { get; internal set; }
        public int Remaining { get; internal set; }
        public MineralDepositStage Stage { get; internal set; }
        public int DrillId { get; internal set; }
        public double DrillProgress { get; internal set; }
    }
}
