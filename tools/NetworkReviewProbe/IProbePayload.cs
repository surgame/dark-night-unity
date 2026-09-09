namespace DarkNights.Tools.NetworkReview
{
    /// <summary>模拟未注册 MemoryPack formatter 的接口边界，不替代 YYGC 或 FishNet。</summary>
    public interface IProbePayload
    {
        int Value { get; set; }
    }
}
