using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkNights.View.Expedition
{
    /// <summary>
    /// 单个纹理远景层的有界平铺网格；只渲染相机范围，独立横纵视差并量化到原生像素。
    /// 由Journey或编辑器预览宿主释放，不参与受光、碰撞或运行随机状态。
    /// </summary>
    internal sealed class SurfaceTextureBackdrop : IDisposable
    {
        private readonly SurfaceBackdropLayer layer;
        private readonly GameObject root;
        private readonly Material material;
        private readonly Mesh mesh;
        private readonly List<Vector3> vertices = new List<Vector3>(512);
        private readonly List<Vector2> uvs = new List<Vector2>(512);
        private readonly List<Color> colors = new List<Color>(512);
        private readonly List<int> triangles = new List<int>(768);
        private readonly bool cellSpace;
        private readonly bool fillVerticalEdges;

        internal SurfaceTextureBackdrop(Transform parent, SurfaceBackdropLayer source, int order, bool cellSpace,
            bool fillVerticalEdges)
        {
            layer = source.Capture(); this.cellSpace = cellSpace;
            this.fillVerticalEdges = fillVerticalEdges;
            root = new GameObject("Surface backdrop " + order);
            root.transform.SetParent(parent, false);
            var filter = root.AddComponent<MeshFilter>();
            var renderer = root.AddComponent<MeshRenderer>();
            mesh = new Mesh { name = "Visible surface backdrop tiles" }; mesh.MarkDynamic(); filter.sharedMesh = mesh;
            material = new Material(Shader.Find("Sprites/Default")) { mainTexture = layer.Texture };
            renderer.sharedMaterial = material; renderer.sortingOrder = order;
        }

        internal void Show(bool value) => root.SetActive(value && layer.Enabled && layer.Texture != null);

        internal void Render(Camera camera, float ground)
        {
            if (!root.activeSelf) return;
            Vector3 center = root.transform.InverseTransformPoint(camera.transform.position);
            float unit = cellSpace ? 1 : .16f;
            float pixel = unit / 16;
            float halfHeight = camera.orthographicSize / Mathf.Abs(root.transform.lossyScale.y);
            float halfWidth = halfHeight * camera.aspect;
            float period = layer.Pixels.width / 16f * unit * layer.PixelScale;
            float height = layer.Pixels.height / 16f * unit * layer.PixelScale;
            float offset = center.x * (1 - layer.HorizontalParallax);
            float bottom = ground + layer.BottomCells * unit + (center.y - ground) * (1 - layer.VerticalParallax);
            bottom = Mathf.Round(bottom / pixel) * pixel;
            float top = bottom + height;
            int first = Mathf.FloorToInt((center.x - halfWidth - offset) / period) - 1;
            int last = Mathf.CeilToInt((center.x + halfWidth - offset) / period) + 1;
            if (last - first > 128) throw new InvalidOperationException("远景可见平铺超过128段。");
            float u0 = layer.Pixels.x / (float)layer.Texture.width, u1 = layer.Pixels.xMax / (float)layer.Texture.width;
            float v0 = layer.Pixels.y / (float)layer.Texture.height, v1 = layer.Pixels.yMax / (float)layer.Texture.height;
            Color color = QualitySettings.activeColorSpace == ColorSpace.Linear ? layer.Tint.linear : layer.Tint;
            vertices.Clear(); uvs.Clear(); colors.Clear(); triangles.Clear();
            for (int tile = first; tile <= last; tile++)
            {
                float x0 = Mathf.Round((offset + tile * period) / pixel) * pixel, x1 = x0 + period;
                Quad(x0, x1, bottom, top, u0, u1, v0, v1, color);
                // 天空边缘只采裁片内的首尾像素行，延展覆盖视口而不拉伸或重复渐变。
                if (fillVerticalEdges && center.y + halfHeight > top)
                {
                    float edge = (layer.Pixels.yMax - .5f) / layer.Texture.height;
                    Quad(x0, x1, top, center.y + halfHeight + pixel, u0, u1, edge, edge, color);
                }
                if (fillVerticalEdges && center.y - halfHeight < bottom)
                {
                    float edge = (layer.Pixels.y + .5f) / layer.Texture.height;
                    Quad(x0, x1, center.y - halfHeight - pixel, bottom, u0, u1, edge, edge, color);
                }
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }

        private void Quad(float left, float right, float bottom, float top, float u0, float u1, float v0, float v1, Color color)
        {
            int i = vertices.Count;
            vertices.Add(new Vector3(left, bottom, 1)); vertices.Add(new Vector3(right, bottom, 1));
            vertices.Add(new Vector3(right, top, 1)); vertices.Add(new Vector3(left, top, 1));
            uvs.Add(new Vector2(u0, v0)); uvs.Add(new Vector2(u1, v0));
            uvs.Add(new Vector2(u1, v1)); uvs.Add(new Vector2(u0, v1));
            for (int c = 0; c < 4; c++) colors.Add(color);
            triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
            triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2);
        }

        public void Dispose()
        {
            foreach (var asset in new UnityEngine.Object[] { root, mesh, material })
                if (Application.isPlaying) UnityEngine.Object.Destroy(asset); else UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}
