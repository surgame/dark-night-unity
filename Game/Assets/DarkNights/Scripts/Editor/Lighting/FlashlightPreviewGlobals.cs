using System;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>离屏调参预览的 Shader 全局值保护；同一主线程绘制前捕获正式光场绑定，绘制及退休后完整恢复。</summary>
    public sealed class FlashlightPreviewGlobals : IDisposable
    {
        private static readonly string[] FloatNames = { "_DNLightingAmbient", "_DNRelief", "_DNLightingActive", "_DNLightingReady", "_DNLightingBackend" };
        private static readonly string[] VectorNames = { "_DNKnownBounds", "_DNShadowSettings", "_DNLightRect", "_DNDistanceRect" };
        private static readonly string[] TextureNames = { "_DNLightCells", "_DNLightField", "_DNLightDirection", "_DNWallDistance" };
        private readonly float[] floats = new float[FloatNames.Length];
        private readonly Vector4[] vectors = new Vector4[VectorNames.Length];
        private readonly Texture[] textures = new Texture[TextureNames.Length];
        private readonly Matrix4x4 matrix;
        public FlashlightPreviewGlobals()
        {
            for (int i = 0; i < floats.Length; i++) floats[i] = Shader.GetGlobalFloat(FloatNames[i]);
            for (int i = 0; i < vectors.Length; i++) vectors[i] = Shader.GetGlobalVector(VectorNames[i]);
            for (int i = 0; i < textures.Length; i++) textures[i] = Shader.GetGlobalTexture(TextureNames[i]);
            matrix = Shader.GetGlobalMatrix("_DNMapWorldToLocal");
        }
        public void Dispose()
        {
            for (int i = 0; i < floats.Length; i++) Shader.SetGlobalFloat(FloatNames[i], floats[i]);
            for (int i = 0; i < vectors.Length; i++) Shader.SetGlobalVector(VectorNames[i], vectors[i]);
            for (int i = 0; i < textures.Length; i++) Shader.SetGlobalTexture(TextureNames[i], textures[i]);
            Shader.SetGlobalMatrix("_DNMapWorldToLocal", matrix);
        }
    }
}
