using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>背景栅格唯一坐标合同；源行向下，AnyRules 的 V 向上，像素中心对应逻辑格左上角减半格的偏移。</summary>
    public static class TerrainVisualCoordinates
    {
        public static float LocalX(int pixel, int density = 8) => (pixel + .5f) / density - .5f;
        public static float LocalV(int rowPixel, int density = 8) => .5f - (rowPixel + .5f) / density;
        /// <summary>冻结背景的地表天际线；每个原生像素列取首个真实坡形实心像素，空列取地图底部，不随采矿重烘焙。</summary>
        public static ushort[] SurfaceSkyline(BackgroundBakeDescriptor source)
        {
            int density = BackgroundBakeDescriptor.RasterPixelsPerCell;
            var skyline = new ushort[source.Width * density];
            for (int pixel = 0; pixel < skyline.Length; pixel++)
            {
                skyline[pixel] = (ushort)(source.Height * density);
                int x = pixel / density;
                for (int row = 0; row < source.Height; row++)
                {
                    if (source.Material(x, row) == 0) continue;
                    var shape = (TerrainCellShape)source.Shape(x, row);
                    for (int py = 0; py < density; py++)
                    {
                        if (!TerrainShapeGeometry.Contains(shape, (pixel % density + .5f) / density,
                            1 - (py + .5f) / density)) continue;
                        skyline[pixel] = (ushort)(row * density + py);
                        break;
                    }
                    if (skyline[pixel] != source.Height * density) break;
                }
            }
            return skyline;
        }
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
