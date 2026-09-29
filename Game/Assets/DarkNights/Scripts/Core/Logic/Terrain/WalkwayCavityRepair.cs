using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>为步道两侧被分开的原有空腔开凿局部绕行孔；保留步道支撑、保护格和基岩，超出预算即拒绝候选。</summary>
    internal static class WalkwayCavityRepair
    {
        private const int W = TerrainGenerationSettings.Width, H = TerrainGenerationSettings.Height;

        public static bool TryRepair(byte[] before, byte[] cells, bool[] protection, bool[] soft,
            bool[] support, Func<bool> cancelled)
        {
            var original = TerrainCavityConnectivity.Label(before, cancelled);
            int excavated = 0;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                if (cancelled != null && cancelled()) throw new OperationCanceledException();
                var labels = TerrainCavityConnectivity.Label(cells, cancelled);
                if (!FindSplit(original, labels, out int from, out int to)) return true;
                var parents = new int[cells.Length];
                Array.Fill(parents, -1);
                var queue = new Queue<int>();
                for (int i = 0; i < cells.Length; i++)
                    if (labels[i] == from) { parents[i] = i; queue.Enqueue(i); }
                int end = -1;
                while (queue.Count > 0 && end < 0)
                {
                    int i = queue.Dequeue(), x = i % W, y = i / W;
                    if (x > 3) Add(i - 1);
                    if (x < W - 4) Add(i + 1);
                    if (y > 0) Add(i - W);
                    if (y < H - 4) Add(i + W);
                    void Add(int next)
                    {
                        if (parents[next] >= 0 || support[next] || protection[next] || cells[next] == 8) return;
                        parents[next] = i;
                        if (labels[next] == to) end = next;
                        else queue.Enqueue(next);
                    }
                }
                if (end < 0) return false;
                while (parents[end] != end)
                {
                    int cx = end % W, cy = end / W;
                    for (int y = Math.Max(0, cy - 2); y <= Math.Min(H - 4, cy + 2); y++)
                        for (int x = Math.Max(3, cx - 2); x <= Math.Min(W - 4, cx + 2); x++)
                        {
                            int i = y * W + x;
                            if (support[i] || protection[i] || cells[i] == 8) continue;
                            if (cells[i] != 0 && ++excavated > 256) return false;
                            cells[i] = 0; soft[i] = false;
                        }
                    end = parents[end];
                }
            }
            return false;
        }

        private static bool FindSplit(int[] before, int[] after, out int from, out int to)
        {
            var components = new Dictionary<int, int>();
            for (int i = 0; i < before.Length; i++)
            {
                if (before[i] == 0 || after[i] == 0) continue;
                if (components.TryGetValue(before[i], out from))
                {
                    if (from != after[i]) { to = after[i]; return true; }
                }
                else components.Add(before[i], after[i]);
            }
            from = to = 0; return false;
        }
    }
}
