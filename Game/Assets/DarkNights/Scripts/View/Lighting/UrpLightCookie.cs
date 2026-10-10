using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkNights.View.Lighting
{
    /// <summary>单个 URP 灯的有界形状纹理；仅参数变化时重建，移动与旋转复用，退休同时释放 Sprite 和纹理。</summary>
    public sealed class UrpLightCookie
    {
        private const int Size = 256;
        private readonly Texture2D texture;
        private readonly Func<float, float, float, float, float, float, bool, float> profile;
        private readonly Color32[] pixels = new Color32[Size * Size];
        private Vector4 installed;
        private bool installedDirectional;
        private bool installedNear;
        public Sprite Sprite { get; private set; }

        public UrpLightCookie(Func<float, float, float, float, float, float, bool, float> evaluator)
        {
            profile = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
            { name = "Dark Nights URP light profile", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        }

        public void Update(LightEmitterData source, ExplorationLightSettings settings, bool near = false)
        {
            float range = near ? source.Rules.NearRange : source.Rules.Range;
            float cone = source.Rules.Cone;
            float aperture = source.Rules.ApertureWidth;
            float feather = source.Rules.ConeFeather;
            var shape = new Vector4(range, cone, aperture, feather);
            bool directional = source.Directional && !near;
            if (Sprite != null && installed == shape && installedDirectional == directional && installedNear == near) return;
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                float forward = ((x + .5f) / Size * 2 - 1) * range;
                float side = ((y + .5f) / Size * 2 - 1) * range;
                float energy = profile(forward, side, range, cone, aperture, feather, directional);
                if (near) energy = Mathf.Pow(energy, 1.1f / 1.15f);
                byte encoded = (byte)Mathf.RoundToInt(255 * energy);
                pixels[y * Size + x] = new Color32(encoded, encoded, encoded, 255);
            }
            texture.SetPixels32(pixels); texture.Apply(false, false);
            if (Sprite != null) Destroy(Sprite);
            Sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), Size / (range * 2), 0, SpriteMeshType.FullRect);
            Sprite.name = "Dark Nights URP beam"; Sprite.hideFlags = HideFlags.DontSave;
            installed = shape; installedDirectional = directional;
            installedNear = near;
        }

        public void Dispose() { if (Sprite != null) Destroy(Sprite); Sprite = null; Destroy(texture); }
        private static void Destroy(Object value)
        { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    }
}
