using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 单个 YYGC 工位唯一拥有的存量、生产时钟和占用关系；农田来源使用稳定实体 ID。
    /// 只有当前会话的业务事务可写，客户端不会推进生产。
    /// </summary>
    [MemoryPackable, StateData]
    public partial record WorksiteState
    {
        public uint Sequence { get; set; }
        public int Id { get; internal set; }
        public string PlacementKey { get; internal set; }
        public float X { get; internal set; }
        public int WorkerId { get; internal set; }
        public int Amount { get; internal set; }
        public double Progress { get; internal set; }
        public int Variant { get; internal set; }
        public int FarmId { get; internal set; }
    }
}
