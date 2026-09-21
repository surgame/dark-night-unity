using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>由最终格子重建静态洞穴光场；传播穿过岩体快速衰减，灯位是确定性的表现装饰，不构成采集或交互对象。</summary>
    internal static class CaveLightField
    {
        internal static Color32[] Build(Color32[] cells, int width, int height, Color32[] ores = null,
            byte[] devices = null, int lightFalloff = 12, int rockLightLoss = 96)
        {
            var warm = new byte[cells.Length]; var cold = new byte[cells.Length];
            for (int i = 0; i < cells.Length; i++) cells[i].b = 0;
            if (devices == null) for (int y = 48; y < height - 5; y += 19) for (int x = 14; x < width - 8; x += 23)
            {
                for (int dy = -8; dy <= 8; dy++)
                {
                    int row = y + dy, i = row * width + x;
                    if (cells[i].r != 0 || cells[i + width].r == 0 || cells[i - width].r != 0) continue;
                    warm[i] = 255; cells[i].b = 1; break;
                }
            }
            for (int y = 0; y < height - 1; y++) for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (devices != null) warm[i] = devices[i];
                if (ores != null && ores[i].g > 0)
                { if (ores[i].r > 0) warm[i] = 220; else cold[i] = 230; }
            }
            Spread(warm, cells, width, height, lightFalloff, rockLightLoss);
            Spread(cold, cells, width, height, lightFalloff, rockLightLoss);
            var result = new Color32[cells.Length];
            for (int i = 0; i < cells.Length; i++) result[i] = new Color32(warm[i], cold[i], 0, 255);
            return result;
        }
        private static void Spread(byte[] light, Color32[] cells, int width, int height, int emptyLoss, int rockLoss)
        {
            emptyLoss = Mathf.Clamp(emptyLoss, 6, 24); rockLoss = Mathf.Clamp(rockLoss, 48, 160);
            var queue = new Queue<int>();
            for (int i = 0; i < light.Length; i++) if (light[i] > 0) queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % width, y = i / width;
                if (x > 0) Visit(i - 1, false); if (x < width - 1) Visit(i + 1, false);
                if (y > 0) Visit(i - width, false); if (y < height - 1) Visit(i + width, false);
                if (x > 0 && y > 0) Visit(i - width - 1, true);
                if (x < width - 1 && y > 0) Visit(i - width + 1, true);
                if (x > 0 && y < height - 1) Visit(i + width - 1, true);
                if (x < width - 1 && y < height - 1) Visit(i + width + 1, true);
                void Visit(int next, bool diagonal)
                {
                    int loss = cells[next].r == 0 ? emptyLoss : rockLoss;
                    if (diagonal) loss = (loss * 7 + 4) / 5;
                    int value = light[i] - loss;
                    if (value <= light[next]) return;
                    light[next] = (byte)value; queue.Enqueue(next);
                }
            }
        }
    }
}
