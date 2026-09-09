using MemoryPack;

namespace DarkNights.Tools.NetworkReview
{
    /// <summary>提供已生成具体 formatter 的对照数据，以区分接口注册与具体类型注册。</summary>
    [MemoryPackable]
    public partial class ProbePayload : IProbePayload
    {
        public int Value { get; set; }
    }
}
