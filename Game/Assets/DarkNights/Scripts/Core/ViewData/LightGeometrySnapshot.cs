using System;

namespace DarkNights.Core.ViewData
{
    /// <summary>局部地形照明任务独占的格数据；128 表示已知，64 表示岩体，低四位为同源坡形编号。</summary>
    public sealed class LightGeometrySnapshot
    {
        public const int SamplesPerCell = 4;
        public int Width { get; }
        public int Height { get; }
        public byte[] Cells { get; }

        public LightGeometrySnapshot(int width, int height, byte[] ownedCells)
        {
            if (width <= 0 || height <= 0 || width > 192 || height > 192 || ownedCells == null || ownedCells.Length != width * height)
                throw new ArgumentException("照明几何必须是有界、独占的局部格副本。");
            Width = width; Height = height; Cells = ownedCells;
        }
    }
}
