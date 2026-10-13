using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>单次地质事件的唯一YYGC状态；只保留阶段、模拟时间与地图身份，地形格子仍由SessionTerrain拥有。实例退休或重新装配时不重放已触发事件。</summary>
    [MemoryPackable, StateData]
    public partial record GeologyEventState
    {
        public uint Sequence { get; set; }
        public int Phase { get; internal set; }
        public double Clock { get; internal set; }
        public string WorldId { get; internal set; } = "";
        public ulong MapEpoch { get; internal set; }
        public string Reason { get; internal set; } = "";
    }
}
