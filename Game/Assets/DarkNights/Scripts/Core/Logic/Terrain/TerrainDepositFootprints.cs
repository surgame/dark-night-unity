using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>最终地形后的确定性矿床分配；不修改前景或随机数流，格身份固定，保护格/基岩/重叠拒绝，总容量严格守恒。</summary>
    public static class TerrainDepositFootprints
    {
        public const int NominalCells = 8;
        public const int MaximumCellsPerDeposit = 64;
        public const int MaximumWorldCells = 4096;
        public const int Version = 1;
        private const int W = TerrainGenerationSettings.Width, H = TerrainGenerationSettings.Height;
        private static readonly int[] Dx = { 1, 0, -1, 0 }, Dy = { 0, 1, 0, -1 };

        public static TerrainDepositBlueprint[] Build(IEnumerable<TerrainDepositBlueprint> anchors,
            byte[] materials, bool[] protection, string seed, int minimumRow = 0)
        {
            if (anchors == null || materials == null || protection == null || materials.Length != W * H ||
                protection.Length != materials.Length || seed == null || minimumRow < 0 || minimumRow >= H)
                throw new ArgumentException("矿床分配必须使用完整最终地形。");
            var occupied = new HashSet<int>();
            var result = new List<TerrainDepositBlueprint>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var anchor in anchors.OrderBy(value => value?.Id, StringComparer.Ordinal))
            {
                if (anchor == null || !ids.Add(anchor.Id)) throw new ArgumentException("矿床锚点重复或为空。");
                bool Allowed(int x, int y) => x >= 0 && x < W && y >= minimumRow && y < H &&
                    materials[y * W + x] != 8 && !protection[y * W + x] && !occupied.Contains(y * W + x);
                int first = -1;
                for (int radius = 0; radius <= 12 && first < 0; radius++)
                    for (int y = anchor.Y - radius; y <= anchor.Y + radius && first < 0; y++)
                        for (int x = anchor.X - radius; x <= anchor.X + radius; x++)
                            if (Math.Abs(x - anchor.X) + Math.Abs(y - anchor.Y) == radius && Allowed(x, y))
                            { first = y * W + x; break; }
                if (first < 0) throw new InvalidOperationException("矿床附近没有可分配的非保护格：" + anchor.Id);
                uint hash = 2166136261;
                foreach (char value in seed + "|" + anchor.Id) hash = unchecked((hash ^ value) * 16777619);
                var pending = new Queue<int>(); var visited = new HashSet<int>(); var footprint = new List<int>();
                pending.Enqueue(first); visited.Add(first);
                int target = Math.Min(NominalCells, anchor.Capacity);
                while (pending.Count > 0 && footprint.Count < target)
                {
                    int index = pending.Dequeue(), x = index % W, y = index / W;
                    if (!Allowed(x, y)) continue;
                    footprint.Add(index);
                    for (int step = 0; step < 4; step++)
                    {
                        int direction = (step + (int)(hash & 3)) & 3;
                        int nx = x + Dx[direction], ny = y + Dy[direction];
                        if (Allowed(nx, ny) && visited.Add(ny * W + nx)) pending.Enqueue(ny * W + nx);
                    }
                }
                if (occupied.Count + footprint.Count > MaximumWorldCells) throw new InvalidOperationException("矿层超过世界格预算。");
                footprint.Sort();
                var cells = new TerrainMineralCell[footprint.Count];
                for (int i = 0; i < cells.Length; i++)
                {
                    int index = footprint[i]; occupied.Add(index);
                    cells[i] = new TerrainMineralCell(index % W, -index / W,
                        anchor.Capacity / cells.Length + (i < anchor.Capacity % cells.Length ? 1 : 0));
                }
                result.Add(new TerrainDepositBlueprint(anchor.Id, anchor.RoomKind, first % W, first / W,
                    anchor.Rarity, anchor.Capacity, cells));
            }
            return result.ToArray();
        }
    }
}
