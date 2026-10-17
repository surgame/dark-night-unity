using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Experiments.MiningExploration
{
    /// <summary>
    /// 在真实最终前景上计算空腔沿路距离与围岩厚度，只供R1开发端分档和选位。
    /// 它不模拟重力、身体净空、跳跃或梯具，不能替代正式角色通行验收。
    /// </summary>
    public sealed class MiningRouteField
    {
        private readonly int[] distances;
        private readonly int[] thickness;
        private readonly int[] owners;
        private readonly MineMapDefinition map;
        public byte[] Materials { get; }
        public bool[] Protection { get; }
        public int Entry { get; }
        public int MaximumDistance { get; }
        public int FirstThreshold => Math.Max(1, MaximumDistance * 30 / 100);
        public int SecondThreshold => Math.Max(2, MaximumDistance * 65 / 100);

        public MiningRouteField(PlayableTerrain terrain, MineMapDefinition map, int minimumRow)
        {
            this.map = map;
            Materials = terrain.CopyMaterials(); Protection = terrain.CopyProtection();
            distances = Enumerable.Repeat(-1, Materials.Length).ToArray();
            thickness = Enumerable.Repeat(-1, Materials.Length).ToArray();
            owners = Enumerable.Repeat(-1, Materials.Length).ToArray();
            var camp = map.Rooms.First(r => r.Id == "base/camp");
            int wanted = (camp.Floor + map.OffsetY - 3) * 320 + camp.X + map.OffsetX;
            Entry = ClosestAir(wanted, minimumRow);
            Fill(distances, new[] { Entry }, i => Materials[i] == 0 && i / 320 >= minimumRow);
            MaximumDistance = Math.Max(1, distances.Max());
            Fill(thickness, Enumerable.Range(0, Materials.Length).Where(i => Materials[i] == 0), i => true);
            for (int y = minimumRow; y < 189; y++)
                for (int x = 3; x < 317; x++) owners[y * 320 + x] = FindOwner(x, y);
        }

        private int ClosestAir(int wanted, int minimumRow)
        {
            int x = wanted % 320, y = wanted / 320;
            for (int radius = 0; radius <= 16; radius++)
                for (int row = Math.Max(minimumRow, y - radius); row <= Math.Min(188, y + radius); row++)
                    for (int column = Math.Max(3, x - radius); column <= Math.Min(316, x + radius); column++)
                        if (Materials[row * 320 + column] == 0) return row * 320 + column;
            throw new InvalidOperationException("作者入口附近没有可用空腔。");
        }

        private static void Fill(int[] field, IEnumerable<int> seeds, Func<int, bool> allowed)
        {
            var queue = new Queue<int>();
            foreach (int seed in seeds) { field[seed] = 0; queue.Enqueue(seed); }
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int next in Neighbors(current))
                    if (field[next] < 0 && allowed(next))
                    { field[next] = field[current] + 1; queue.Enqueue(next); }
            }
        }

        private int FindOwner(int x, int y)
        {
            int owner = -1; double best = 2;
            for (int i = 0; i < map.Rooms.Length; i++)
            {
                var room = map.Rooms[i];
                double dx = (x - map.OffsetX - room.X) / (double)(room.Radius + 5);
                double dy = (y - map.OffsetY - room.Floor + room.Height / 2.0) / (room.Height + 4);
                double score = dx * dx + dy * dy;
                if (score < best) { owner = i; best = score; }
            }
            return owner;
        }

        public int RoomDistance(int room)
        {
            var source = map.Rooms[room]; int best = int.MaxValue;
            for (int y = source.Floor - source.Height; y < source.Floor; y++)
                for (int x = source.X - source.Radius; x <= source.X + source.Radius; x++)
                {
                    int d = Distance((y + map.OffsetY) * 320 + x + map.OffsetX);
                    if (d >= 0) best = Math.Min(best, d);
                }
            return best == int.MaxValue ? -1 : best;
        }

        public int Distance(int index) => distances[index];
        public int Thickness(int index) => thickness[index];
        public int Owner(int index) => owners[index];
        public int Tier(int distance) => distance < FirstThreshold ? 0 : distance < SecondThreshold ? 1 : 2;
        public int[] CopyDistances() => (int[])distances.Clone();
        public bool IsWall(int index) => Materials[index] == 2 && !Protection[index];

        public static IEnumerable<int> Neighbors(int index)
        {
            int x = index % 320, y = index / 320;
            if (x < 319) yield return index + 1;
            if (y < 191) yield return index + 320;
            if (x > 0) yield return index - 1;
            if (y > 0) yield return index - 320;
        }
    }
}
