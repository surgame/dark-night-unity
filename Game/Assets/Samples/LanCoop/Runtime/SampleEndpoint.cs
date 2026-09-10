using System;
using FishNet.Connection;
using FishNet.Object;

namespace DarkNights.Samples.LanCoop.Runtime
{
    /// <summary>与 YYGC owned Sender 同 Prefab 的回执组件；只向请求者确认，不继承或重织框架 RPC。</summary>
    public sealed class SampleEndpoint : NetworkBehaviour
    {
        public event Action<long, string, int, int> ResultReceived;

        [FishNet.Object.TargetRpc]
        public void Reply(NetworkConnection connection, long sequence, string code, int epoch, int revision)
        {
            ResultReceived?.Invoke(sequence, code, epoch, revision);
        }
    }
}
