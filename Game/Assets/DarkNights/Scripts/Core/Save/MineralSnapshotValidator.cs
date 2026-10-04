using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Save
{
    /// <summary>矿层最终格与初始容量的恢复检查；矿床不是实体，耗尽格可为空，禁止生成新格、换矿种或增加初始储量。</summary>
    internal static class MineralSnapshotValidator
    {
        internal static string Validate(SessionSnapshot snapshot)
        {
            if (snapshot.MineralDeposits.Count != 0) return "矿床实体运行状态已退出 v19 保存合同";
            var data = snapshot.Terrain;
            if (data?.Minerals == null) return "";
            var capacities = new int[data.Count]; var kinds = new byte[data.Count];
            foreach (var deposit in data.Deposits) foreach (var cell in deposit.Cells)
            {
                int index = -cell.V * TerrainGenerationSettings.Width + cell.U;
                capacities[index] = cell.Capacity; kinds[index] = deposit.MineralKind == "gold" ? (byte)2 : (byte)1;
            }
            for (int i = 0; i < data.Count; i++)
                if (data.Minerals.Kind(i) != 0 && (data.Minerals.Kind(i) != kinds[i] || data.Minerals.Reserves(i) > capacities[i]))
                    return "矿层保存与初始矿种或容量不匹配";
            return "";
        }
    }
}
