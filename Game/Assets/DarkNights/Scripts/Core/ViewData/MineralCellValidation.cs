using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.ViewData
{
    /// <summary>矿格的纯合同验证；为完整投影和保存共同拒绝越界、非法墓碑、过量容量及世界重叠，不修改任何对象。</summary>
    public static class MineralCellValidation
    {
        public static bool Validate(IReadOnlyList<MineralCellViewData> cells, int maximumDurability, HashSet<int> occupied)
        {
            if (cells == null || cells.Count < 1 || cells.Count > 64 || maximumDurability < 1 || maximumDurability > 1000000) return false;
            long capacity = 0;
            foreach (var cell in cells)
            {
                if (cell.U < 0 || cell.U >= TerrainGenerationSettings.Width || cell.V > 0 || cell.V <= -TerrainGenerationSettings.Height ||
                    cell.Capacity < 1 || cell.Capacity > 1000000 || cell.Remaining < 0 || cell.Remaining > cell.Capacity ||
                    cell.Durability < 0 || cell.Durability > maximumDurability ||
                    (cell.Remaining == 0 ? cell.Durability != 0 || cell.ContentVersion != 2 : cell.Durability < 1 || cell.ContentVersion != 1) ||
                    !occupied.Add(-cell.V * TerrainGenerationSettings.Width + cell.U)) return false;
                capacity += cell.Capacity;
            }
            return capacity <= 1000000 && occupied.Count <= 4096;
        }
    }
}
