using FishNet.Connection;
using DarkNights.Runtime.Session;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 服务端为一个已握手连接持有的业务能力与 owned 回执入口，客户端字段不能构造此映射。
    /// 每秒预算与 Ready 超时归本次连接所有；断开时移除，营地任务由权威服务保留。
    /// </summary>
    internal sealed class SessionPeer
    {
        public NetworkConnection Network { get; }
        public SessionConnection Authority { get; set; }
        public bool IsHost { get; }
        public PlayerEndpoint Endpoint { get; }
        public double ReadyDeadline { get; set; }
        public int ReadyEpoch { get; set; }
        public int Requests { get; set; }

        public SessionPeer(NetworkConnection network, bool host, PlayerEndpoint endpoint, double joinedAt)
        {
            Network = network;
            IsHost = host;
            Endpoint = endpoint;
            ReadyDeadline = joinedAt + 30;
        }
    }
}
