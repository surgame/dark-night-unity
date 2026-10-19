using System;
using System.Collections;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>当前前景已提交Alpha的不透明位图；仅同步真实上传补丁，不读GPU或写地图，随所属表现宿主退休。</summary>
    internal sealed class CaveForegroundCoverage
    {
        private const int Width = 2560, Page = 256, PagePixels = Page * Page;
        private readonly BitArray opaque = new BitArray(Width * 1536);
        private readonly int[] counts = new int[60];
        private readonly bool[] initialized = new bool[60];
        public Func<int, bool> CanPresent { get; set; }
        public long Revision { get; private set; }
        public bool HasPage(int key) => initialized[key];
        public bool FullyOpaque(int key) => initialized[key] && counts[key] == PagePixels;

        /// <summary>在同一主线程上传提交中更新；首次完整页之前不把零散像素当作整页已知覆盖。</summary>
        public void Commit(RectInt rect, byte[] pixels)
        {
            if (rect.xMin < 0 || rect.yMin < 0 || rect.xMax > Width || rect.yMax > 1536 ||
                rect.width <= 0 || rect.height <= 0 || pixels == null || pixels.Length != rect.width * rect.height * 4)
                throw new ArgumentException("前景覆盖补丁无效。");
            for (int y = 0; y < rect.height; y++) for (int x = 0; x < rect.width; x++)
            {
                int wx = rect.x + x, wy = rect.y + y, index = wy * Width + wx;
                bool value = pixels[(y * rect.width + x) * 4 + 3] >= 128;
                if (opaque[index] == value) continue;
                opaque[index] = value;
                counts[wy / Page * 10 + wx / Page] += value ? 1 : -1;
            }
            for (int py = rect.yMin / Page; py <= (rect.yMax - 1) / Page; py++)
                for (int px = rect.xMin / Page; px <= (rect.xMax - 1) / Page; px++)
                    if (rect.xMin <= px * Page && rect.xMax >= (px + 1) * Page &&
                        rect.yMin <= py * Page && rect.yMax >= (py + 1) * Page)
                        initialized[py * 10 + px] = true;
            Revision++;
        }
    }
}
