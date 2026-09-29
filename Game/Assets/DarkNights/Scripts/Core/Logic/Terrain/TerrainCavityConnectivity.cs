using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>比较修改前后空腔分量及洞室中心；原本连通的空格不得因后置步骤被分成两块。</summary>
    internal static class TerrainCavityConnectivity
    {
        private const int W = TerrainGenerationSettings.Width;
        private const int H = TerrainGenerationSettings.Height;

        public static bool PreservesRooms(byte[] before, byte[] after, IReadOnlyList<TerrainRoom> rooms, Func<bool> cancelled)
        {
            int[] original = Label(before, cancelled);
            int[] changed = Label(after, cancelled);
            var mapping = new Dictionary<int, int>();
            for (int index = 0; index < original.Length; index++)
            {
                int component = original[index], destination = changed[index];
                if (component == 0 || destination == 0) continue;
                if (mapping.TryGetValue(component, out int previous))
                {
                    if (previous != destination) return false;
                }
                else mapping.Add(component, destination);
            }
            foreach (var room in rooms)
            {
                int x = room.X, y = room.Y;
                if (x < 0 || x >= W || y < 0 || y >= H) continue;
                int index = y * W + x, component = original[index];
                if (component == 0) continue;
                if (changed[index] == 0) return false;
                if (mapping.TryGetValue(component, out int destination) && destination != changed[index]) return false;
            }
            return true;
        }

        private static int[] Label(byte[] cells, Func<bool> cancelled)
        {
            int[] labels = new int[cells.Length], queue = new int[cells.Length];
            int next = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != 0 || labels[i] != 0) continue;
                if (cancelled != null && cancelled()) throw new OperationCanceledException("连通检查已取消。");
                int label = ++next, read = 0, count = 0;
                labels[i] = label; queue[count++] = i;
                while (read < count)
                {
                    int current = queue[read++], x = current % W, y = current / W;
                    if (x > 0) Add(current - 1);
                    if (x + 1 < W) Add(current + 1);
                    if (y > 0) Add(current - W);
                    if (y + 1 < H) Add(current + W);
                }
                void Add(int target)
                {
                    if (cells[target] != 0 || labels[target] != 0) return;
                    labels[target] = label; queue[count++] = target;
                }
            }
            return labels;
        }
    }
}
