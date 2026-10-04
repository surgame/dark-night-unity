namespace DarkNights.Core.Config.Terrain
{
    /// <summary>
    /// 由地图生成器输出的静态矿床标记。它只描述对象初始位置、容量和稀有度，
    /// 不保存运行时已采集容量；运行时进度归 YYGC MineralDeposit 状态所有。
    /// </summary>
    public sealed class TerrainDepositBlueprint
    {
        public string Id { get; }
        public string RoomKind { get; }
        public int X { get; }
        public int Y { get; }
        public string Rarity { get; }
        public int Capacity { get; }
        public string MineralKind => RoomKind == "boss" || Rarity == "rare" ? "gold" : "iron";
        public System.Collections.Generic.IReadOnlyList<TerrainMineralCell> Cells { get; }

        public TerrainDepositBlueprint(string id, string roomKind, int x, int y, string rarity, int capacity,
            System.Collections.Generic.IReadOnlyList<TerrainMineralCell> cells = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(roomKind) ||
                string.IsNullOrWhiteSpace(rarity) || capacity <= 0)
                throw new System.ArgumentException("矿床静态标记无效。");
            Id = id; RoomKind = roomKind; X = x; Y = y; Rarity = rarity; Capacity = capacity;
            var values = new System.Collections.Generic.List<TerrainMineralCell>(cells ?? new[] { new TerrainMineralCell(x, -y, capacity) });
            var coordinates = new System.Collections.Generic.HashSet<int>(); long total = 0;
            if (values.Count == 0 || values.Count > 64) throw new System.ArgumentException("矿床格数超出预算。");
            foreach (var cell in values)
            {
                if (cell.Capacity < 1 || !coordinates.Add(-cell.V * TerrainGenerationSettings.Width + cell.U))
                    throw new System.ArgumentException("矿床初始分配重复或容量无效。");
                total += cell.Capacity;
            }
            if (total != capacity) throw new System.ArgumentException("矿床格容量必须严格守恒。");
            Cells = values.AsReadOnly();
        }
    }
}
