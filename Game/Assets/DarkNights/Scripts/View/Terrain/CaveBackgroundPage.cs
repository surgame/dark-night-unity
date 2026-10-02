using System;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>一页独立排序的背景素材；各层拥有贴图与材质，共用页面几何及本地图光场，不释放共享输入。</summary>
    internal sealed class CaveBackgroundPage : IDisposable
    {
        private readonly Texture2D[] textures;
        private readonly Material[] materials;
        private readonly Mesh mesh;
        private readonly GameObject root;
        public int LastUsed { get; set; }
        public CaveBackgroundPage(int x, int row, byte[][] pixels, Material wall, Transform parent, CaveBackgroundStyle style)
        {
            textures = new Texture2D[pixels.Length]; materials = new Material[pixels.Length];
            root = new GameObject("Static cave page " + x + "," + row); root.transform.SetParent(parent, false);
            float left = x * 32 - .5f, top = .5f - row * 32;
            mesh = new Mesh { name = "Static cave page quad" };
            mesh.vertices = new[] { new Vector3(left, top - 32, 1), new Vector3(left + 32, top - 32, 1),
                new Vector3(left + 32, top, 1), new Vector3(left, top, 1) };
            // 源行向下；Unity 首行 texel 对应 UV 底边，因此页面 UV 反向映射。
            mesh.uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateBounds();
            int[] orders = { style.NearOrder, style.MiddleOrder, style.DeepOrder, style.FarOrder };
            bool[] enabled = { style.Near, style.Middle, style.Deep, true };
            var shader = style.LayerShader ?? Shader.Find("DarkNights/CaveBackgroundLayer");
            if (shader == null) throw new InvalidOperationException("缺少背景素材 Shader。");
            for (int i = 0; i < pixels.Length; i++)
            {
                textures[i] = new Texture2D(256, 256, TextureFormat.RGBA32, false, false)
                { name = "Cave art page layer " + i, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                textures[i].LoadRawTextureData(pixels[i]); textures[i].Apply(false, true);
                materials[i] = new Material(shader) { name = "Cave authored background layer " + i };
                materials[i].SetTexture("_MainTex", textures[i]);
                materials[i].SetTexture("_CaveLight", wall.GetTexture("_CaveLight"));
                materials[i].SetFloat("_Ambient", style.BackgroundAmbient);
                materials[i].SetMatrix("_MapWorldToLocal", parent.worldToLocalMatrix);
                var layer = new GameObject("Cave background layer " + i); layer.transform.SetParent(root.transform, false);
                layer.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = layer.AddComponent<MeshRenderer>(); renderer.sharedMaterial = materials[i]; renderer.sortingOrder = orders[i];
                layer.SetActive(enabled[i]);
            }
        }
        public void Show(bool value) => root.SetActive(value);
        public void Dispose()
        {
            DestroyOwned(root); DestroyOwned(mesh);
            foreach (var material in materials) DestroyOwned(material);
            foreach (var texture in textures) DestroyOwned(texture);
        }
        private static void DestroyOwned(UnityEngine.Object value)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
