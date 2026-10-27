using System;
using System.Globalization;
using UnityEngine;

namespace DarkNights.View.Expedition
{
    /// <summary>一层远景的作者配置；像素裁切不改原图，高度以泊位为基准，视差为相机移动的保留比例。</summary>
    [Serializable]
    public sealed class SurfaceBackdropLayer
    {
        public Texture2D Texture;
        public RectInt Pixels;
        [Range(0, 1)] public float HorizontalParallax = .15f;
        [Range(0, 1), Tooltip("纵向位移保留比例：1固定于地表，0完全跟随镜头。山林默认1，以横向视差分层。")]
        public float VerticalParallax = 1;
        public float BottomCells = -6;
        [Range(.5f, 4)] public float PixelScale = 1;
        public Color Tint = Color.white;
        public bool Enabled = true;
        // 仅保留旧资源的序列化字段；山林不再延展裁片底行。
        [HideInInspector] public bool FillBelow;

        public SurfaceBackdropLayer Capture()
        {
            var copy = (SurfaceBackdropLayer)MemberwiseClone();
            copy.HorizontalParallax = FiniteClamp(HorizontalParallax, 0, 1, .15f);
            copy.VerticalParallax = FiniteClamp(VerticalParallax, 0, 1, 1);
            copy.BottomCells = FiniteClamp(BottomCells, -48, 48, -6);
            copy.PixelScale = FiniteClamp(PixelScale, .5f, 4, 1);
            if (Texture != null && (Pixels.x < 0 || Pixels.y < 0 || Pixels.width < 1 || Pixels.height < 1 ||
                Pixels.xMax > Texture.width || Pixels.yMax > Texture.height))
                throw new ArgumentException("远景裁切超出原图：" + Texture.name);
            return copy;
        }

        public string Identity => string.Join(",", Texture == null ? "" : Texture.name, Pixels.x, Pixels.y,
            Pixels.width, Pixels.height, N(HorizontalParallax), N(VerticalParallax), N(BottomCells), N(PixelScale),
            N(Tint.r), N(Tint.g), N(Tint.b), N(Tint.a), Enabled, FillBelow);
        private static string N(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        private static float FiniteClamp(float v, float min, float max, float fallback) =>
            float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp(v, min, max);
    }
}
