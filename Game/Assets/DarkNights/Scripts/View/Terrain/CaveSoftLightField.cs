using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>洞穴表现专用的圆形柔光场；读取权威设备副本，按岩体遮挡衰减。矿床视觉待重做，不产生矿粒或矿光。</summary>
    internal static class CaveSoftLightField
    {
        public static Color32[] Build(Color32[] cells, int width, int height, byte[] devices)
        {
            var warm = new float[cells.Length];
            for (int i = 0; i < cells.Length; i++) cells[i].b = 0;
            if (devices == null) for (int y = 48; y < height - 5; y += 19) for (int x = 14; x < width - 8; x += 23)
            {
                for (int dy = -8; dy <= 8; dy++)
                {
                    int row = y + dy, i = row * width + x;
                    if (cells[i].r != 0 || cells[i + width].r == 0 || cells[i - width].r != 0) continue;
                    cells[i].b = 1; Add(warm, x, row, 8.375f, 1); break;
                }
            }
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (devices != null && devices[i] != 0) Add(warm, x, y, 8.375f, devices[i] / 255f);
            }
            var result = new Color32[cells.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(warm[i]) * 255), 0, 0, 255);
            return result;

            void Add(float[] target, int sx, int sy, float radius, float strength)
            {
                int range = Mathf.CeilToInt(radius);
                for (int y = Mathf.Max(0, sy - range); y <= Mathf.Min(height - 1, sy + range); y++)
                    for (int x = Mathf.Max(0, sx - range); x <= Mathf.Min(width - 1, sx + range); x++)
                    {
                        float dx = x - sx, dy = y - sy, distance = Mathf.Sqrt(dx * dx + dy * dy);
                        if (distance >= radius) continue;
                        // 三束有限宽度采样使遮挡边缘柔和；每条采样按穿过岩体的长度连续衰减。
                        float attenuation = Ray(0) * .5f + (Ray(-.65f) + Ray(.65f)) * .25f;
                        target[y * width + x] += Mathf.Pow(1 - distance / radius, 1.7f) * strength * attenuation;
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
    }
}
