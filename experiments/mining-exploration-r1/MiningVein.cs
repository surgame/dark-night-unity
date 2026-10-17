using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Experiments.MiningExploration
{
    /// <summary>
    /// 开发端生成的一条不可变矿脉；露头、壁内格及揭露顺序在导出前冻结，游戏中不重新抽签。
    /// 仅为样板静态元数据，实际矿格复用TerrainDepositBlueprint，不持有采掘进度。
    /// </summary>
    public sealed class MiningVein
    {
        public string Id { get; }
        public int Head { get; }
        public int Room { get; }
        public int Distance { get; }
        public int Tier { get; }
        public bool Interior { get; }
        public string Kind { get; }
        public int Capacity { get; }
        public IReadOnlyList<int> Cells { get; }
        public int WallCount => Cells.Count - 1;

        public MiningVein(string id, int head, int room, int distance, int tier, bool interior,
            string kind, int capacity, IEnumerable<int> cells)
        {
            var copy = cells.ToArray();
            if (copy.Length < 1 || copy.Length > 64 || copy[0] != head ||
                capacity < copy.Length || copy.Distinct().Count() != copy.Length)
                throw new ArgumentException("矿脉露头、占格或储量不满足静态约束。");
            Id = id; Head = head; Room = room; Distance = distance; Tier = tier;
            Interior = interior; Kind = kind; Capacity = capacity;
            Cells = Array.AsReadOnly(copy);
        }

        public MiningVein WithCapacity(int capacity) => new MiningVein(Id, Head, Room, Distance,
            Tier, Interior, Kind, capacity, Cells);

        public TerrainDepositBlueprint ToDeposit()
        {
            var cells = new TerrainMineralCell[Cells.Count];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = new TerrainMineralCell(Cells[i] % 320, -Cells[i] / 320,
                    Capacity / cells.Length + (i < Capacity % cells.Length ? 1 : 0));
            return new TerrainDepositBlueprint(Id, "gallery", Head % 320, Head / 320,
                Kind == "gold" ? "rare" : "common", Capacity, cells);
        }
    }
}
