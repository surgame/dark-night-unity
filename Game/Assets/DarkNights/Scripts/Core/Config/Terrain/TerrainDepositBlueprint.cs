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

        public TerrainDepositBlueprint(string id, string roomKind, int x, int y, string rarity, int capacity)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(roomKind) ||
                string.IsNullOrWhiteSpace(rarity) || capacity <= 0)
                throw new System.ArgumentException("矿床静态标记无效。");
            Id = id; RoomKind = roomKind; X = x; Y = y; Rarity = rarity; Capacity = capacity;
        }
    }
}
