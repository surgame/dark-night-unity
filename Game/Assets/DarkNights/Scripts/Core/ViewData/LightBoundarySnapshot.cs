using System;
using System.Collections.Generic;

namespace DarkNights.Core.ViewData
{
    /// <summary>冻结地形的外露物理边界及八格空间索引；坐标向右／向下，数组独占且只读，不拥有地图业务状态。</summary>
    public sealed class LightBoundarySnapshot
    {
        public const int PageSize = 8;
        private readonly Segment[] segments;
        private readonly int[][] pages;
        public int Width { get; }
        public int Height { get; }
        public int Columns => (Width + PageSize - 1) / PageSize;
        public int Count => segments.Length;
        public Segment this[int index] => segments[index];
        public IReadOnlyList<int> Page(int x, int y) => Array.AsReadOnly(pages[y * Columns + x]);
        public int PageCount(int x, int y) => pages[y * Columns + x].Length;
        public int PageSegment(int x, int y, int index) => pages[y * Columns + x][index];

        public LightBoundarySnapshot(int width, int height, Segment[] ownedSegments, int[][] ownedPages)
        {
            Width = width; Height = height;
            segments = ownedSegments ?? throw new ArgumentNullException(nameof(ownedSegments));
            pages = ownedPages ?? throw new ArgumentNullException(nameof(ownedPages));
        }

        /// <summary>一条合并后的同向共线物理边；法线指向已确认空气，门洞和转角保持独立，身份只服务表现缓存。</summary>
        public readonly struct Segment
        {
            public readonly float Ax, Ay, Bx, By, Nx, Ny;
            public readonly long Id;
            public Segment(float ax, float ay, float bx, float by, float nx, float ny, long id)
            { Ax = ax; Ay = ay; Bx = bx; By = by; Nx = nx; Ny = ny; Id = id; }
        }
    }
}
