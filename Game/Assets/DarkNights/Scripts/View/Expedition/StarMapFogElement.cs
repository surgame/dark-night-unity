using System;
using DarkNights.Core.ViewData;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.View.Expedition
{
    /// <summary>
    /// 权威勘测位图的本地低分辨率迷雾贴图；仅在位图或视口变化时重绘。
    /// 拥有并释放唯一运行时纹理，不缓存客户端自推断航迹，不反写探索状态。
    /// </summary>
    internal sealed class StarMapFogElement : Image, IDisposable
    {
        private Texture2D texture;
        private string encoded;
        private Vector2 center, size;
        private float scale;

        internal StarMapFogElement()
        {
            AddToClassList("star-map-layer"); pickingMode = PickingMode.Ignore; scaleMode = ScaleMode.StretchToFill;
        }

        internal void Present(NavigationSurveyData survey, Vector2 location, float factor, Vector2 dimensions)
        {
            if (dimensions.x < 1 || dimensions.y < 1) return;
            if (encoded == survey.Encoded && center == location && scale == factor && size == dimensions) return;
            encoded = survey.Encoded; center = location; scale = factor; size = dimensions;
            int width = Mathf.Clamp(Mathf.CeilToInt(size.x / 5), 1, 256);
            int height = Mathf.Clamp(Mathf.CeilToInt(size.y / 5), 1, 160);
            if (texture == null || texture.width != width || texture.height != height)
            {
                Release(); texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { name = "Star map survey fog", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                image = texture;
            }
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float wx = center.x + ((x + .5f) / width - .5f) * size.x / scale;
                    float wy = center.y + ((y + .5f) / height - .5f) * size.y / scale;
                    if (survey.IsRevealed(wx, wy)) continue;
                    int grain = unchecked((int)Math.Floor(wx * 3) * 73856093 ^ (int)Math.Floor(wy * 3) * 19349663) & 7;
                    pixels[y * width + x] = new Color32((byte)(27 + grain), (byte)(36 + grain), (byte)(57 + grain * 2), 255);
                }
            texture.SetPixels32(pixels); texture.Apply(false, false);
        }

        internal void ClearSurvey() { encoded = null; image = null; Release(); }
        private void Release()
        {
            if (texture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture); else UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
        }
        public void Dispose() => ClearSurvey();
    }
}
