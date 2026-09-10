using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace YYGC.IdentityValidation
{
    /// <summary>宿主显式提供的多态格式器输入；由 MemoryPack 生成，不改框架旧 wire 或类型 Tag。</summary>
    [MemoryPackUnionFormatter(typeof(IStateData))]
    [MemoryPackUnion(7, typeof(IdentityProbeState))]
    public partial class IdentityProbeFormatter { }
}
