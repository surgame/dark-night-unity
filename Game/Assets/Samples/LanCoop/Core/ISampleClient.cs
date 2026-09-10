namespace DarkNights.Samples.LanCoop.Core
{
    /// <summary>Sample 展示层的窄入口；组合层注入网络实现，界面只能提交意图及读取冻结副本。</summary>
    public interface ISampleClient
    {
        CampReplica Replica { get; }
        string Status { get; }
        string LastResult { get; }
        bool Ready { get; }
        void Connect(bool host, string address, ushort port);
        void Disconnect();
        void Send(SampleOperation operation, int entity = 1, int fault = 0, bool repeat = false);
    }
}
