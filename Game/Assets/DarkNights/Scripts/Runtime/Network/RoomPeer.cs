using System.Net;
using FishNet.Connection;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 房主拥有的一个待出发席位和内存票据；UDP端点、票据及实际连接共同校验，不接受自报玩家槽位。
    /// 游戏世界身份仍由原SessionServer分配，断线只保留有界恢复窗口，不向存档泄露票据。
    /// </summary>
    internal sealed class RoomPeer
    {
        internal string Id, Name, JoinRequest;
        internal byte[] Key;
        internal IPEndPoint Endpoint;
        internal long Sequence;
        internal bool Ready;
        internal double Seen;
        internal NetworkConnection Connection;
    }
}
