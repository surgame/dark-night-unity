using System;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>分开安装基础和装饰的一页背景；各层独占纹理与材质，共享页几何和只读光场，随缓存退休。</summary>
    internal sealed class CaveBackgroundPage : IDisposable
    {
        private readonly Texture2D[] textures = new Texture2D[4];
        private readonly Material[] materials = new Material[4];
        private readonly GameObject[] layers = new GameObject[4];
        private readonly Mesh mesh;
        private readonly GameObject root;
        private readonly Material wall;
        private readonly Transform parent;
        private readonly CaveBackgroundStyle style;
        public int LastUsed { get; set; }
        public bool HasBase => textures[3] != null;
        public bool HasDecorations { get; private set; }
        public long Bytes { get; private set; }
        public CaveBackgroundPage(int x, int row, byte[][] pixels, Material wall, Transform parent, CaveBackgroundStyle style)
            : this(x, row, pixels, wall, parent, style, 0) { }

        public CaveBackgroundPage(int x, int row, byte[][] pixels, Material wall, Transform parent, CaveBackgroundStyle style, int firstLayer)
        {
            this.wall = wall; this.parent = parent; this.style = style;
            root = new GameObject("Static cave page " + x + "," + row); root.transform.SetParent(parent, false);
            float left = x * 32 - .5f, top = .5f - row * 32;
            mesh = new Mesh { name = "Static cave page quad" };
            mesh.vertices = new[] { new Vector3(left, top - 32, 1), new Vector3(left + 32, top - 32, 1),
                new Vector3(left + 32, top, 1), new Vector3(left, top, 1) };
            // 源行向下；Unity 首行 texel 对应 UV 底边，因此页面 UV 反向映射。
            mesh.uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateBounds();
            Install(pixels, firstLayer);
        }

        /// <summary>只添加尚未生成的装饰；基础后壁保持原实例，拆墙不替换已有页。</summary>
        public void InstallDecorations(byte[][] pixels) => Install(pixels, 0);

        private void Install(byte[][] pixels, int firstLayer)
        {
            int[] orders = { style.NearOrder, style.MiddleOrder, style.DeepOrder, style.FarOrder };
            bool[] enabled = { style.Near, style.Middle, style.Deep, true };
            var shader = style.LayerShader ?? Shader.Find("DarkNights/CaveBackgroundLayer");
            if (shader == null) throw new InvalidOperationException("缺少背景素材 Shader。");
            for (int p = 0; p < pixels.Length; p++)
            {
                int i = firstLayer + p;
                if (i < 0 || i >= 4 || textures[i] != null) throw new InvalidOperationException("背景层重复安装或索引无效。");
                if (!enabled[i] || (i < 3 && !HasVisiblePixels(pixels[p]))) continue;
                textures[i] = new Texture2D(256, 256, TextureFormat.RGBA32, false, false)
                { name = "Cave art page layer " + i, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                textures[i].LoadRawTextureData(pixels[p]); textures[i].Apply(false, true);
                materials[i] = new Material(shader) { name = "Cave authored background layer " + i };
                materials[i].SetTexture("_MainTex", textures[i]);
                materials[i].SetTexture("_CaveLight", wall.GetTexture("_CaveLight"));
                materials[i].SetFloat("_Ambient", style.BackgroundAmbient);
                materials[i].SetMatrix("_MapWorldToLocal", parent.worldToLocalMatrix);
                Texture rock = wall.HasProperty("_RockSurface") ? wall.GetTexture("_RockSurface") : null;
                materials[i].SetFloat("_BackgroundOcclusion", rock is RenderTexture ? 1 : 0);
                if (rock is RenderTexture)
                {
                    materials[i].SetTexture("_RockSurface", rock);
                    materials[i].SetTexture("_CaveMap", wall.GetTexture("_CaveMap"));
                }
                var layer = new GameObject("Cave background layer " + i); layer.transform.SetParent(root.transform, false);
                layers[i] = layer;
                layer.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = layer.AddComponent<MeshRenderer>(); renderer.sharedMaterial = materials[i]; renderer.sortingOrder = orders[i];
                layer.SetActive(enabled[i]);
                Bytes += pixels[p].Length;
            }
            if (firstLayer == 0) HasDecorations = true;
        }
        public void Show(bool value) => root.SetActive(value);
        private static bool HasVisiblePixels(byte[] pixels)
        {
            for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] != 0) return true;
            return false;
        }
        public void DropDecorations()
        {
            for (int i = 0; i < 3; i++)
            {
                if (textures[i] != null) Bytes -= 256 * 256 * 4;
                if (layers[i] != null) layers[i].SetActive(false);
                DestroyOwned(layers[i]); layers[i] = null;
                DestroyOwned(textures[i]); DestroyOwned(materials[i]); textures[i] = null; materials[i] = null;
            }
            HasDecorations = false;
        }
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
