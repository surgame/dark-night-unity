using System;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>一页无矿、无动态光的三层纹理及后壁几何；主线程创建和销毁，颜色按 sRGB 解码一次，Alpha 保持直通。</summary>
    internal sealed class CaveBackgroundPage : IDisposable
    {
        private readonly Texture2D[] textures = new Texture2D[3];
        private readonly Material material;
        private readonly Mesh mesh;
        private readonly GameObject root;
        public int LastUsed { get; set; }
        public CaveBackgroundPage(int x, int row, byte[][] pixels, Material wall, Transform parent, CaveBackgroundStyle style)
        {
            material = new Material(wall) { name = "Static cave background page" };
            string[] names = { "_StrataNear", "_StrataMiddle", "_StrataDeep" };
            for (int i = 0; i < 3; i++)
            {
                textures[i] = new Texture2D(256, 256, TextureFormat.RGBA32, false, false)
                { name = names[i], filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                textures[i].LoadRawTextureData(pixels[i]); textures[i].Apply(false, true);
                material.SetTexture(names[i], textures[i]);
            }
            material.SetVector("_StrataPage", new Vector4(x * 32, row * 32, 32, 32));
            material.SetVector("_StrataEnabled", new Vector4(style.Near ? 1 : 0, style.Middle ? 1 : 0, style.Deep ? 1 : 0, 1));
            root = new GameObject("Static cave page " + x + "," + row); root.transform.SetParent(parent, false);
            float left = x * 32 - .5f, top = .5f - row * 32;
            mesh = new Mesh { name = "Static cave page quad" };
            mesh.vertices = new[] { new Vector3(left, top - 32, 1), new Vector3(left + 32, top - 32, 1),
                new Vector3(left + 32, top, 1), new Vector3(left, top, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = -99;
        }
        public void Show(bool value) => root.SetActive(value);
        public void Dispose()
        {
            UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(mesh); UnityEngine.Object.Destroy(material);
            foreach (var texture in textures) UnityEngine.Object.Destroy(texture);
        }
    }
}
