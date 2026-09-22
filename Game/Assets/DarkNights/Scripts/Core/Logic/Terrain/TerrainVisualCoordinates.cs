using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>背景栅格唯一坐标合同；源行向下，AnyRules 的 V 向上，像素中心对应逻辑格左上角减半格的偏移。</summary>
    public static class TerrainVisualCoordinates
    {
        public static float LocalX(int pixel, int density = 8) => (pixel + .5f) / density - .5f;
        public static float LocalV(int rowPixel, int density = 8) => .5f - (rowPixel + .5f) / density;
        public static byte[] Rasterize(BackgroundBakeDescriptor source)
        {
            int density = BackgroundBakeDescriptor.RasterPixelsPerCell, width = source.Width * density;
            var pixels = new byte[width * source.Height * density];
            for (int row = 0; row < source.Height; row++) for (int x = 0; x < source.Width; x++)
            {
                if (source.Material(x, row) == 0) continue;
                var shape = (TerrainCellShape)source.Shape(x, row);
                for (int py = 0; py < density; py++) for (int px = 0; px < density; px++)
                    if (TerrainShapeGeometry.Contains(shape, (px + .5f) / density, 1 - (py + .5f) / density))
                        pixels[(row * density + py) * width + x * density + px] = 1;
            }
            return pixels;
        }
    }
}
