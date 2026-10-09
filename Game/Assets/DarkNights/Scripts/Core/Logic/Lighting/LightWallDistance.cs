using System;
using DarkNights.Core.ViewData;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Core.Logic.Lighting
{
    /// <summary>只对冻结格计算岩体到空气的保守距离；未知格保持不可用，取消不留下共享缓冲或池引用。</summary>
    public static class LightWallDistance
    {
        public static float[] Build(LightGeometrySnapshot snapshot, Func<bool> cancelled)
        {
            int scale = LightGeometrySnapshot.SamplesPerCell;
            int width = snapshot.Width * scale, height = snapshot.Height * scale;
            var distances = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                if (cancelled?.Invoke() == true) throw new OperationCanceledException();
                for (int x = 0; x < width; x++)
                {
                    byte cell = snapshot.Cells[y / scale * snapshot.Width + x / scale];
                    bool solid = (cell & 128) == 0 || (cell & 64) != 0 && TerrainShapeGeometry.Contains(
                        (TerrainCellShape)(cell & 15), (x % scale + .5f) / scale, 1 - (y % scale + .5f) / scale);
                    distances[y * width + x] = solid ? 1024 : 0;
                }
            }
            for (int y = 0; y < height; y++)
            {
                if (cancelled?.Invoke() == true) throw new OperationCanceledException();
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (x > 0) distances[i] = Math.Min(distances[i], distances[i - 1] + 1);
                    if (y > 0) distances[i] = Math.Min(distances[i], distances[i - width] + 1);
                    if (x > 0 && y > 0) distances[i] = Math.Min(distances[i], distances[i - width - 1] + 1.5f);
                    if (x + 1 < width && y > 0) distances[i] = Math.Min(distances[i], distances[i - width + 1] + 1.5f);
                }
            }
            for (int y = height - 1; y >= 0; y--)
            {
                if (cancelled?.Invoke() == true) throw new OperationCanceledException();
                for (int x = width - 1; x >= 0; x--)
                {
                    int i = y * width + x;
                    if (x + 1 < width) distances[i] = Math.Min(distances[i], distances[i + 1] + 1);
                    if (y + 1 < height) distances[i] = Math.Min(distances[i], distances[i + width] + 1);
                    if (x + 1 < width && y + 1 < height) distances[i] = Math.Min(distances[i], distances[i + width + 1] + 1.5f);
                    if (x > 0 && y + 1 < height) distances[i] = Math.Min(distances[i], distances[i + width - 1] + 1.5f);
                }
            }
            // 上偏估计和半采样偏置使限制更保守；直射还会独立检查首次碰撞后的路径深度。
            for (int i = 0; i < distances.Length; i++) distances[i] /= scale;
            return distances;
        }
    }
}
