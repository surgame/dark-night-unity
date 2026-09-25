using System;
using System.Collections.Generic;
using System.Threading;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>前景只读格缓存到有界像素补丁；每次冻结输出矩形及完整依赖 Halo，工作线程不读活地图。</summary>
    internal sealed class CaveLocalRockGeometry
    {
        private const int CellWidth = 320, CellHeight = 192, PixelScale = 8;
        private readonly string seed;
        private readonly int stoneSize;
        private readonly CaveOutlineSettings outline;
        private readonly RoundedClusterModifier modifier;
        private readonly CaveModifierStack modifiers;
        private readonly CancellationToken cancellation;
        private readonly byte[] materials = new byte[CellWidth * CellHeight], shapes = new byte[CellWidth * CellHeight];
        public int DirtyPixelRadius { get; }
        public bool Initialized { get; private set; }
        public CaveLocalRockGeometry(string seed, CaveOutlineSettings outline, CaveModifierStack modifiers,
            int stoneSize, CancellationToken cancellation)
        {
            this.seed = seed; this.outline = outline; this.stoneSize = stoneSize; this.cancellation = cancellation;
            this.modifiers = modifiers ?? CaveModifierStack.Empty; modifier = this.modifiers.RequireLocalRoundedCluster();
            DirtyPixelRadius = CaveRockBaker.DistanceCap + (outline?.Reach ?? 0) +
                (modifier?.DependencyRadiusPixels ?? 0) + PixelScale;
        }
        public void Replace(Color32[] cells)
        {
            if (cells == null || cells.Length != materials.Length) throw new ArgumentException("岩壁格缓存尺寸无效。");
            for (int i = 0; i < cells.Length; i++) { materials[i] = cells[i].r; shapes[i] = cells[i].g; }
            Initialized = true;
        }
        public void ApplyCells(Color32[] cells, IReadOnlyList<int> changedIndexes)
        {
            if (!Initialized || cells == null || changedIndexes == null) return;
            foreach (int index in changedIndexes)
            {
                if (index < 0 || index >= materials.Length) throw new ArgumentOutOfRangeException(nameof(changedIndexes));
                materials[index] = cells[index].r; shapes[index] = cells[index].g;
            }
        }
        public Func<byte[]> CapturePageBake(int pageKey) => CaptureRegionBake(PageRect(pageKey));
        /// <summary>冻结任意页内输出矩形；同步小补丁与后台整页使用完全相同的算法。</summary>
        public Func<byte[]> CaptureRegionBake(RectInt output, Action checkpoint = null)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!Initialized || output.width <= 0 || output.height <= 0 || output.xMin < 0 || output.yMin < 0 ||
                output.xMax > 2560 || output.yMax > 1536) throw new ArgumentException("岩壁补丁范围无效。");
            int halo = CaveRockBaker.DistanceCap + (outline?.Reach ?? 0) + (modifier?.DependencyRadiusPixels ?? 0) + 3;
            int minX = Math.Max(0, (output.xMin - halo) / PixelScale - 1), minY = Math.Max(0, (output.yMin - halo) / PixelScale - 1);
            int maxX = Math.Min(CellWidth, (int)Math.Ceiling((output.xMax + halo) / (double)PixelScale) + 1);
            int maxY = Math.Min(CellHeight, (int)Math.Ceiling((output.yMax + halo) / (double)PixelScale) + 1);
            int width = maxX - minX, height = maxY - minY;
            var frozenMaterials = new byte[checked(width * height)]; var frozenShapes = new byte[frozenMaterials.Length];
            for (int y = 0; y < height; y++)
            {
                Array.Copy(materials, (minY + y) * CellWidth + minX, frozenMaterials, y * width, width);
                Array.Copy(shapes, (minY + y) * CellWidth + minX, frozenShapes, y * width, width);
            }
            return () =>
            {
                void Check() { cancellation.ThrowIfCancellationRequested(); checkpoint?.Invoke(); }
                Check();
                var region = CaveModifiedTerrain.BakeRockRegion(Solid, 2560, 1536, seed, outline, modifiers,
                    output.x, output.y, output.width, output.height, CaveRockBaker.DistanceCap, 43 * PixelScale, Check);
                return CaveRockBaker.BakeRegion(region, output.x, output.y, output.width, output.height, seed, stoneSize, Check);
                bool Solid(int x, int y)
                {
                    if (x < 0 || y < 0 || x >= 2560 || y >= 1536) return false;
                    int cellX = x / PixelScale - minX, cellY = y / PixelScale - minY;
                    if (cellX < 0 || cellY < 0 || cellX >= width || cellY >= height)
                        throw new InvalidOperationException("冻结补丁未覆盖算法读取 Halo。");
                    int i = cellY * width + cellX;
                    return frozenMaterials[i] != 0 && TerrainShapeGeometry.Contains((TerrainCellShape)frozenShapes[i],
                        (x % PixelScale + .5f) / PixelScale, 1 - (y % PixelScale + .5f) / PixelScale);
                }
            };
        }
        public RectInt ChangedRect(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= materials.Length) throw new ArgumentOutOfRangeException(nameof(cellIndex));
            int x = cellIndex % CellWidth * PixelScale, y = cellIndex / CellWidth * PixelScale;
            int left = Math.Max(0, x - DirtyPixelRadius), top = Math.Max(0, y - DirtyPixelRadius);
            return new RectInt(left, top, Math.Min(2560, x + PixelScale + DirtyPixelRadius) - left,
                Math.Min(1536, y + PixelScale + DirtyPixelRadius) - top);
        }
        public void AddAffectedPages(int cellIndex, HashSet<int> pages)
        {
            var rect = ChangedRect(cellIndex);
            for (int y = rect.yMin / 256; y <= (rect.yMax - 1) / 256; y++)
                for (int x = rect.xMin / 256; x <= (rect.xMax - 1) / 256; x++) pages.Add(y * 10 + x);
        }
        internal static RectInt PageRect(int key)
        {
            if (key < 0 || key >= 60) throw new ArgumentOutOfRangeException(nameof(key));
            return new RectInt(key % 10 * 256, key / 10 * 256, 256, 256);
        }
    }
}
