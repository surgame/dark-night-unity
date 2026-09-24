using System.Collections.Generic;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>洞穴有限半径柔光的确定性计算；局部更新只重算受岩体遮挡或光源资格影响的 32 格页。</summary>
    internal static class CaveSoftLightField
    {
        private const float Radius = 8.375f;
        private const int FirstMarkerX = 14, MarkerStepX = 23, FirstAnchorY = 48, MarkerStepY = 19;

        public static Color32[] Build(Color32[] cells, int width, int height, byte[] devices)
        {
            var sources = FindStaticSources(cells, width, height, devices == null);
            for (int i = 0; i < cells.Length; i++) cells[i].b = 0;
            foreach (int source in sources) cells[source].b = 1;
            return BuildRegion(cells, width, height, devices, 0, 0, width, height, sources);
        }

        /// <summary>局部光照安全点同步静态测试光源标记；变化页与色彩贡献采用同一源资格。</summary>
        public static void SyncMarkers(Color32[] cells, int width, int height, byte[] devices,
            HashSet<int> previous, Dictionary<int, int> anchors, IReadOnlyCollection<int> changedCells, HashSet<int> changedPages)
        {
            if (cells == null || previous == null || anchors == null || changedPages == null)
                throw new System.ArgumentNullException();
            if (devices != null)
            {
                foreach (int index in previous) Set(index, 0);
                previous.Clear(); anchors.Clear(); return;
            }
            if (changedCells == null)
            {
                foreach (int index in previous) Set(index, 0);
                anchors.Clear(); previous.Clear();
                for (int anchorY = FirstAnchorY; anchorY < height - 5; anchorY += MarkerStepY)
                for (int x = FirstMarkerX; x < width - 8; x += MarkerStepX)
                {
                    int index = FindStaticSourceAt(cells, width, height, x, anchorY);
                    if (index < 0) continue;
                    anchors[AnchorKey(x, anchorY, width)] = index;
                    previous.Add(index); Set(index, 1);
                }
                return;
            }
            var affected = new HashSet<int>();
            foreach (int index in changedCells)
            {
                int x = index % width, row = index / width;
                if (x < FirstMarkerX || (x - FirstMarkerX) % MarkerStepX != 0) continue;
                int first = Math.Max(FirstAnchorY, row - 9);
                int last = Math.Min(height - 6, row + 9);
                for (int anchorY = FirstAnchorY + CeilDiv(first - FirstAnchorY, MarkerStepY) * MarkerStepY;
                    anchorY <= last; anchorY += MarkerStepY)
                    affected.Add(AnchorKey(x, anchorY, width));
            }
            foreach (int key in affected)
            {
                if (anchors.TryGetValue(key, out int old)) { anchors.Remove(key); previous.Remove(old); Set(old, 0); }
                int anchorX = key % width, anchorY = key / width;
                int next = FindStaticSourceAt(cells, width, height, anchorX, anchorY);
                if (next < 0) continue;
                anchors[key] = next; previous.Add(next); Set(next, 1);
            }
            void Set(int index, byte value)
            {
                if (cells[index].b == value) return;
                cells[index].b = value; changedPages.Add(index / width / 32 * ((width + 31) / 32) + index % width / 32);
            }
        }

        private static int FindStaticSourceAt(Color32[] cells, int width, int height, int x, int anchorY)
        {
            for (int dy = -8; dy <= 8; dy++)
            {
                int row = anchorY + dy, index = row * width + x;
                if (cells[index].r == 0 && cells[index + width].r != 0 && cells[index - width].r == 0) return index;
            }
            return -1;
        }

        private static int AnchorKey(int x, int y, int width) => y * width + x;

        private static int CeilDiv(int value, int divisor) => value <= 0 ? 0 : (value + divisor - 1) / divisor;

        /// <summary>基于整张冻结格图重算一个目标矩形，供单页补丁使用；矩形外光照像素不会读写。</summary>
        public static Color32[] BuildRegion(Color32[] cells, int width, int height, byte[] devices,
            int left, int top, int regionWidth, int regionHeight, IReadOnlyList<int> staticSources = null)
        {
            var sources = staticSources ?? FindStaticSources(cells, width, height, devices == null);
            var warm = new float[checked(regionWidth * regionHeight)];
            foreach (int index in sources) Add(index % width, index / width, 1);
            if (devices != null)
                for (int i = 0; i < devices.Length; i++) if (devices[i] != 0) Add(i % width, i / width, devices[i] / 255f);
            var result = new Color32[warm.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(warm[i]) * 255), 0, 0, 255);
            return result;

            void Add(int sx, int sy, float strength)
            {
                int range = Mathf.CeilToInt(Radius), minX = Mathf.Max(left, sx - range), maxX = Mathf.Min(left + regionWidth - 1, sx + range);
                int minY = Mathf.Max(top, sy - range), maxY = Mathf.Min(top + regionHeight - 1, sy + range);
                for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
                {
                    float dx = x - sx, dy = y - sy, distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance >= Radius) continue;
                    float attenuation = Ray(0) * .5f + (Ray(-.65f) + Ray(.65f)) * .25f;
                    warm[(y - top) * regionWidth + x - left] += Mathf.Pow(1 - distance / Radius, 1.7f) * strength * attenuation;
                    float Ray(float offset)
                    {
                        if (distance < .01f) return 1;
                        int steps = Mathf.CeilToInt(distance * 2); float rock = 0;
                        for (int s = 1; s <= steps; s++)
                        {
                            float t = s / (float)steps;
                            int px = Mathf.Clamp(Mathf.RoundToInt(sx + dx * t - dy / distance * offset), 0, width - 1);
                            int py = Mathf.Clamp(Mathf.RoundToInt(sy + dy * t + dx / distance * offset), 0, height - 1);
                            if (cells[py * width + px].r != 0) rock += distance / steps;
                        }
                        return Mathf.Exp(-rock * .65f);
                    }
                }
            }
        }

        private static List<int> FindStaticSources(Color32[] cells, int width, int height, bool includeDefault)
        {
            var result = new List<int>(); if (!includeDefault) return result;
            for (int y = FirstAnchorY; y < height - 5; y += MarkerStepY) for (int x = FirstMarkerX; x < width - 8; x += MarkerStepX)
                for (int dy = -8; dy <= 8; dy++)
                {
                    int row = y + dy, index = row * width + x;
                    if (cells[index].r != 0 || cells[index + width].r == 0 || cells[index - width].r != 0) continue;
                    result.Add(index); break;
                }
            return result;
        }
    }
}
