using System;

namespace DarkNights.Entry.Terrain
{
    /// <summary>独立地图验收进程的报告；属于测试诊断，不参加正式协议或游戏存档。</summary>
    [Serializable]
    public sealed class TerrainProbeReport
    {
        public string Role;
        public string Failure;
        public bool Success;
        public bool Reconnected;
        public bool FirstEmpty;
        public bool SecondEmpty;
        public int Accepted;
        public int Rejected;
        public ulong Revision;
        public long Bytes;
        public long Scans;
        public int RejectedPackets;
        public int Pending;
        public bool ReplicaClosed;
    }
}
