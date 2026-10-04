using System.Collections.Generic;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>地图接线的只读诊断字段；仅开发构建读取已有游标，不扫描地图或改变就绪与租约。</summary>
    internal static class TerrainNetworkDiagnostics
    {
        internal static IReadOnlyDictionary<string, string> Capture(SessionTerrainNetwork network) => new Dictionary<string, string>
        {
            ["游戏 Epoch"] = network.Epoch.ToString(),
            ["地图世界"] = network.Replica == null ? "未连接" : network.Replica.World.WorldId + ":" + network.Replica.World.Epoch,
            ["流 Commit"] = network.Replica?.CommitId.ToString() ?? "0",
            ["数据就绪"] = network.DataReady.ToString(),
            ["背景参考"] = network.Background?.ReferenceHash ?? "未安装",
            ["矿层区域"] = network.Minerals?.Region.ToString() ?? "未连接",
            ["矿层 Commit"] = network.Minerals?.Replica?.CommitId.ToString() ?? "0"
        };
    }
}
