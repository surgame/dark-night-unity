using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace YYGC.IdentityValidation
{
    /// <summary>受控联机夹具的实际状态载荷；双方固定类型 Tag，业务值用于验证快照和后续更新。</summary>
    [MemoryPackable]
    public partial class IdentityProbeState : IStateData
    {
        public uint Sequence { get; set; }
        public int Value { get; set; }
        public void CopyFrom(IStateData stateData) { var source = (IdentityProbeState)stateData; Sequence = source.Sequence; Value = source.Value; }
        public void OnReturnToPool() { Sequence = 0; Value = 0; }
    }
}
