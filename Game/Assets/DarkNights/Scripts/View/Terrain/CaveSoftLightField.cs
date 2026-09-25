using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>有限半径柔光；局部计算只读取矩形 Halo 内的光源，保留确定的源遍历和累加顺序。</summary>
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
        public static void SyncMarkers(Color32[] cells, int width, int height, byte[] devices,
            HashSet<int> previous, Dictionary<int, int> anchors, IReadOnlyCollection<int> changedCells, HashSet<int> changedPages)
        {
            if (cells == null || previous == null || anchors == null || changedPages == null) throw new ArgumentNullException();
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
                    anchors[anchorY * width + x] = index; previous.Add(index); Set(index, 1);
                }
                return;
            }
            var affected = new HashSet<int>();
            foreach (int index in changedCells)
            {
                int x = index % width, row = index / width;
                if (x < FirstMarkerX || (x - FirstMarkerX) % MarkerStepX != 0) continue;
                int first = Math.Max(FirstAnchorY, row - 9), last = Math.Min(height - 6, row + 9);
                for (int anchorY = FirstAnchorY + CeilDiv(first - FirstAnchorY, MarkerStepY) * MarkerStepY;
                    anchorY <= last; anchorY += MarkerStepY) affected.Add(anchorY * width + x);
            }
            foreach (int key in affected)
            {
                if (anchors.TryGetValue(key, out int old)) { anchors.Remove(key); previous.Remove(old); Set(old, 0); }
                int next = FindStaticSourceAt(cells, width, height, key % width, key / width);
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
                int row = anchorY + dy;
                if (row <= 0 || row >= height - 1) continue;
                int index = row * width + x;
                if (cells[index].r == 0 && cells[index + width].r != 0 && cells[index - width].r == 0) return index;
            }
            return -1;
        }
        private static int CeilDiv(int value, int divisor) => value <= 0 ? 0 : (value + divisor - 1) / divisor;
        public static Color32[] BuildRegion(Color32[] cells, int width, int height, byte[] devices,
            int left, int top, int regionWidth, int regionHeight, IReadOnlyList<int> staticSources = null)
        {
            if (cells == null || cells.Length != checked(width * height) || left < 0 || top < 0 || regionWidth < 1 || regionHeight < 1 ||
                left + regionWidth > width || top + regionHeight > height || devices != null && devices.Length != cells.Length)
                throw new ArgumentException("光照区域或格缓存尺寸无效。");
            var sources = staticSources ?? FindStaticSources(cells, width, height, devices == null);
            var warm = new float[checked(regionWidth * regionHeight)];
            foreach (int index in sources) Add(index % width, index / width, 1);
            int range = Mathf.CeilToInt(Radius);
            if (devices != null)
                for (int y = Math.Max(0, top - range); y < Math.Min(height, top + regionHeight + range); y++)
                    for (int x = Math.Max(0, left - range); x < Math.Min(width, left + regionWidth + range); x++)
                        if (devices[y * width + x] != 0) Add(x, y, devices[y * width + x] / 255f);
            var result = new Color32[warm.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(warm[i]) * 255), 0, 0, 255);
            return result;
            void Add(int sx, int sy, float strength)
            {
                int radius = Mathf.CeilToInt(Radius), minX = Mathf.Max(left, sx - radius), maxX = Mathf.Min(left + regionWidth - 1, sx + radius);
                int minY = Mathf.Max(top, sy - radius), maxY = Mathf.Min(top + regionHeight - 1, sy + radius);
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
            var result = new List<int>();
            if (!includeDefault) return result;
            for (int y = FirstAnchorY; y < height - 5; y += MarkerStepY) for (int x = FirstMarkerX; x < width - 8; x += MarkerStepX)
            {
                int index = FindStaticSourceAt(cells, width, height, x, y);
                if (index >= 0) result.Add(index);
            }
            return result;
        }
    }
}
