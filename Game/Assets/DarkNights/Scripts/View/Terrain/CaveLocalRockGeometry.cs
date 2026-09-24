using System;
using System.Collections.Generic;
using System.Threading;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>当前地图像素格到 LocalV2 岩面页的只读适配；仅初次装载复制整图，之后按变化格提供有限 Halo 烘焙。</summary>
    internal sealed class CaveLocalRockGeometry
    {
        private const int CellWidth = 320, CellHeight = 192, PixelScale = 8;
        private readonly string seed;
        private readonly int stoneSize;
        private readonly CaveOutlineSettings outline;
        private readonly RoundedClusterModifier modifier;
        private readonly CaveModifierStack modifiers;
        private readonly CancellationToken cancellation;
        private readonly byte[] materials = new byte[CellWidth * CellHeight];
        private readonly byte[] shapes = new byte[CellWidth * CellHeight];
        private bool initialized;
        public int DirtyPixelRadius { get; }
        public bool Initialized => initialized;

        public CaveLocalRockGeometry(string seed, CaveOutlineSettings outline, CaveModifierStack modifiers,
            int stoneSize, CancellationToken cancellation)
        {
            this.seed = seed; this.outline = outline; this.stoneSize = stoneSize; this.cancellation = cancellation;
            this.modifiers = modifiers ?? CaveModifierStack.Empty; modifier = this.modifiers.RequireLocalRoundedCluster();
            DirtyPixelRadius = CaveRockBaker.DistanceCap + (outline?.Reach ?? 0) +
                (modifier?.DependencyRadiusPixels ?? 0) + PixelScale;
        }

        /// <summary>初始化可重建的逻辑格副本；只在首次区域装载或显式换图时允许整图复制。</summary>
        public void Replace(Color32[] cells)
        {
            if (cells == null || cells.Length != materials.Length) throw new ArgumentException("岩壁格缓存尺寸无效。");
            for (int i = 0; i < cells.Length; i++) { materials[i] = cells[i].r; shapes[i] = cells[i].g; }
            initialized = true;
        }

        /// <summary>只改本批次最终值对应的格缓存，不复制地图数组或重建全图遮罩。</summary>
        public void ApplyCells(Color32[] cells, IReadOnlyList<int> changedIndexes)
        {
            if (!initialized || cells == null || changedIndexes == null) return;
            for (int i = 0; i < changedIndexes.Count; i++)
            {
                int index = changedIndexes[i]; if (index < 0 || index >= materials.Length) throw new ArgumentOutOfRangeException(nameof(changedIndexes));
                materials[index] = cells[index].r; shapes[index] = cells[index].g;
            }
        }

        /// <summary>按页重算基础外轮廓、LocalV2 圆簇及距离材质，仅返回一个固定 256×256 RGBA 补丁。</summary>
        public Func<byte[]> CapturePageBake(int pageKey)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!initialized || pageKey < 0 || pageKey >= 60) throw new InvalidOperationException("岩壁页输入尚未初始化。");
            int left = pageKey % 10 * 256, top = pageKey / 10 * 256;
            int pixelHalo = CaveRockBaker.DistanceCap + (outline?.Reach ?? 0) + (modifier?.DependencyRadiusPixels ?? 0) + 3;
            int minCellX = Math.Max(0, (left - pixelHalo) / PixelScale - 1);
            int minCellY = Math.Max(0, (top - pixelHalo) / PixelScale - 1);
            int maxCellX = Math.Min(CellWidth, (int)Math.Ceiling((left + 256 + pixelHalo) / (double)PixelScale) + 1);
            int maxCellY = Math.Min(CellHeight, (int)Math.Ceiling((top + 256 + pixelHalo) / (double)PixelScale) + 1);
            int width = maxCellX - minCellX, height = maxCellY - minCellY;
            var inputMaterials = new byte[checked(width * height)]; var inputShapes = new byte[inputMaterials.Length];
            for (int y = 0; y < height; y++)
            {
                int source = (minCellY + y) * CellWidth + minCellX, target = y * width;
                Array.Copy(materials, source, inputMaterials, target, width); Array.Copy(shapes, source, inputShapes, target, width);
            }
            var token = cancellation;
            return () =>
            {
                token.ThrowIfCancellationRequested();
                var region = CaveModifiedTerrain.BakeRockRegion(Solid, 2560, 1536, seed, outline, modifiers,
                    left, top, 256, 256, CaveRockBaker.DistanceCap, 43 * PixelScale, token.ThrowIfCancellationRequested);
                return CaveRockBaker.BakeRegion(region, left, top, 256, 256, seed, stoneSize, token.ThrowIfCancellationRequested);
                bool Solid(int x, int y)
                {
                    int cellX = x / PixelScale, cellY = y / PixelScale;
                    if (cellX < 0 || cellY < 0 || cellX >= CellWidth || cellY >= CellHeight) return false;
                    int localX = cellX - minCellX, localY = cellY - minCellY;
                    if (localX < 0 || localY < 0 || localX >= width || localY >= height)
                        throw new InvalidOperationException("岩壁页冻结输入没有覆盖算法读取范围。");
                    int index = localY * width + localX;
                    return inputMaterials[index] != 0 && TerrainShapeGeometry.Contains((TerrainCellShape)inputShapes[index],
                        (x % PixelScale + .5f) / PixelScale, 1 - (y % PixelScale + .5f) / PixelScale);
                }
            };
        }

        /// <summary>返回受格变化、LocalV2 资格与材质距离共同依赖的输出页集合。</summary>
        public void AddAffectedPages(int cellIndex, HashSet<int> pages)
        {
            if (pages == null || cellIndex < 0 || cellIndex >= materials.Length) throw new ArgumentOutOfRangeException(nameof(cellIndex));
            int x = cellIndex % CellWidth * PixelScale, y = cellIndex / CellWidth * PixelScale;
            int minX = Math.Max(0, (x - DirtyPixelRadius) / 256), maxX = Math.Min(9, (x + PixelScale - 1 + DirtyPixelRadius) / 256);
            int minY = Math.Max(0, (y - DirtyPixelRadius) / 256), maxY = Math.Min(5, (y + PixelScale - 1 + DirtyPixelRadius) / 256);
            for (int py = minY; py <= maxY; py++) for (int px = minX; px <= maxX; px++) pages.Add(py * 10 + px);
        }

    }
}
