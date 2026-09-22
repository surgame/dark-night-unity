using FishNet.Broadcast;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>现有可靠会话基线中的有界背景参考块；只传初始材料和形状压缩源，不传纹理或逐格业务命令。</summary>
    public struct TerrainBackgroundChunk : IBroadcast
    {
        public int Epoch;
        public ulong MapEpoch;
        public string WorldId;
        public int Index;
        public byte[] Bytes;
    }
}
