using FishNet.Broadcast;

namespace DarkNights.Runtime.Network
{
    /// <summary>游戏矿层的独立消息通道；载荷仍是原生 AMP1，避免前景适配器将另一层消息交给自己的副本。</summary>
    public struct MineralMapWireMessage : IBroadcast
    {
        public byte[] Bytes;
    }

}
