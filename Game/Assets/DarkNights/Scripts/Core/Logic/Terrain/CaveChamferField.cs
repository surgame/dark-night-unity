using System;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>圆簇融合使用的八邻域浮点有符号距离；逐步舍入到 float 与 H5 Float32Array 一致，不进入碰撞查询。</summary>
    internal static class CaveChamferField
    {
        public static float[] Signed(byte[] mask, int w, int h, Action checkpoint)
        {
            var inside = Distance(mask, w, h, 0, checkpoint); var outside = Distance(mask, w, h, 1, checkpoint);
            for (int i = 0; i < mask.Length; i++) inside[i] = mask[i] != 0 ? -(inside[i] - .5f) : outside[i] - .5f;
            return inside;
        }
        private static float[] Distance(byte[] mask, int w, int h, byte target, Action checkpoint)
        {
            var d = new float[mask.Length]; int inf = w + h; double diagonal = Math.Sqrt(2);
            for (int i = 0; i < d.Length; i++) d[i] = mask[i] == target ? 0 : inf;
            for (int y = 0; y < h; y++)
            {
                checkpoint?.Invoke();
                for (int x = 0; x < w; x++)
                {
                    int p = y * w + x; double v = d[p];
                    if (x > 0) v = Math.Min(v, d[p - 1] + 1.0);
                    if (y > 0) v = Math.Min(v, d[p - w] + 1.0);
                    if (x > 0 && y > 0) v = Math.Min(v, d[p - w - 1] + diagonal);
                    if (x + 1 < w && y > 0) v = Math.Min(v, d[p - w + 1] + diagonal);
                    d[p] = (float)v;
                }
            }
            for (int y = h - 1; y >= 0; y--)
            {
                checkpoint?.Invoke();
                for (int x = w - 1; x >= 0; x--)
                {
                    int p = y * w + x; double v = d[p];
                    if (x + 1 < w) v = Math.Min(v, d[p + 1] + 1.0);
                    if (y + 1 < h) v = Math.Min(v, d[p + w] + 1.0);
                    if (x + 1 < w && y + 1 < h) v = Math.Min(v, d[p + w + 1] + diagonal);
                    if (x > 0 && y + 1 < h) v = Math.Min(v, d[p + w - 1] + diagonal);
                    d[p] = (float)v;
                }
            }
            return d;
        }
    }
}
