using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace DarkNights.Samples.LanCoop.Runtime
{
    /// <summary>YYGC 可靠完整投影；仅持有标量，生成器负责复制与归池，订阅者必须同步复制后再保留。</summary>
    [MemoryPackable, StateData]
    public partial record CampState
    {
        public uint Sequence { get; set; }
        public int Epoch { get; set; }
        public int Revision { get; set; }
        public int Coins { get; set; }
        public int Purchases { get; set; }
        public int Occupant { get; set; }
        public bool Paused { get; set; }
        public int SimulationTicks { get; set; }
    }
}
