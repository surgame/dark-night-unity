using FishNet.Broadcast;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>可靠地图流开始前的世界代次通知；只允许服务端发送，客户端据此清空旧副本并等待完整区块提交。</summary>
    public struct TerrainEpochSignal : IBroadcast
    {
        public int Epoch;
        public string Seed;
        public string WorldId;
        public ulong MapEpoch;
        public int BackgroundBytes;
    }
}
