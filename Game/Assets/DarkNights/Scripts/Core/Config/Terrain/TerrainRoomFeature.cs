using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>洞室生成结果中的静态玩法标签；不携带探索进度、采集进度或权限状态。</summary>
    [Flags]
    public enum TerrainRoomFeature
    {
        None = 0,
        Entry = 1,
        MineralDeposit = 2,
        SoftRock = 4,
        Boss = 8,
        Secret = 16,
        Relic = 32
    }
}
