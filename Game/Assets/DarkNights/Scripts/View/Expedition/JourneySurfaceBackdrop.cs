using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using UnityEngine;

namespace DarkNights.View.Expedition
{
    /// <summary>航程宿主拥有的远山和云层几何；以星球身份稳定生成、按原生像素量化，不消耗玩法随机数，不创建可碰撞对象。</summary>
    public sealed class JourneySurfaceBackdrop : IDisposable
    {
        private const float Pixel = PlayableTerrain.CellPixels / 800f;
        private readonly SurfaceEnvironmentSettings settings;
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly List<Vector3> vertices = new List<Vector3>(2048);
        private readonly List<Color> colors = new List<Color>(2048);
        private readonly List<int> triangles = new List<int>(3072);
        public Mesh Mesh => mesh;

        public JourneySurfaceBackdrop(Transform parent, Material material, SurfaceEnvironmentSettings settings)
        {
            this.settings = settings.Capture();
            root = new GameObject("Journey surface distant scenery", typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Planet pixel ridges and clouds" }; mesh.MarkDynamic();
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = -107;
            root.SetActive(false);
        }

        public void Show(bool visible) => root.SetActive(visible && settings.Enabled);
        public void Render(Camera camera, PlanetDefinition planet, Color skyTint)
        {
            Show(planet != null); if (!root.activeSelf) return;
            vertices.Clear(); colors.Clear(); triangles.Clear();
            float half = camera.orthographicSize * camera.aspect;
            float left = camera.transform.position.x - half, right = camera.transform.position.x + half;
            float bottom = camera.transform.position.y - camera.orthographicSize;
            float ground = (PlayableTerrain.OriginY - (planet.DockRow - .5f) * PlayableTerrain.CellPixels) / 100f;
            uint seed = 2166136261;
            foreach (char c in planet.Id) seed = unchecked((seed ^ c) * 16777619);
            float phase = seed % 1024 * .01f;
            float step = Mathf.Max(Pixel * 2, Mathf.Ceil((right - left) / (128 * Pixel)) * Pixel);
            if (settings.Ridges) for (int layer = 0; layer < 2; layer++)
            {
                float parallax = settings.Parallax * (layer == 0 ? .55f : 1);
                float Height(float x)
                {
                    float wx = x - camera.transform.position.x * (1 - parallax);
                    float wave = .5f + .24f * Mathf.Sin(wx * .57f + phase + layer * 2) + .15f * Mathf.Sin(wx * 1.39f + phase * 1.7f);
                    return Snap(ground + settings.RidgeHeight * .16f * (wave + (layer == 0 ? .4f : .05f)));
                }
                var tint = skyTint * (layer == 0 ? .92f : .73f); tint.a = 1;
                for (float x = Mathf.Floor(left / step) * step; x < right; x += step)
                    Quad(x, x + step, Mathf.Min(bottom, ground - 2), Mathf.Min(bottom, ground - 2), Height(x), Height(x + step), tint, tint);
            }
            if (settings.Clouds)
            {
                float offset = camera.transform.position.x * (1 - settings.Parallax * .35f);
                float period = 8;
                int start = Mathf.FloorToInt((left - offset) / period) - 1;
                int end = Mathf.CeilToInt((right - offset) / period) + 1;
                for (int cloud = start; cloud <= end; cloud++)
                {
                    float center = Snap(offset + cloud * period + Mathf.Sin(cloud * 2.3f + phase));
                    float altitude = Snap(ground + settings.CloudHeight * .16f + Mathf.Sin(cloud + phase) * .4f);
                    float width = 1.1f + .35f * Mathf.Sin(cloud * 3.1f + phase);
                    Color tint = Color.Lerp(skyTint, Linear(new Color32(172, 184, 189, 255)), .35f); tint.a = .3f;
                    Color edge = tint; edge.a = .06f;
                    for (int strip = 0; strip < 16; strip++)
                    {
                        float x0 = Snap(center - width + strip * width / 8), x1 = Snap(center - width + (strip + 1) * width / 8);
                        float height = Snap(.10f + .18f * Mathf.Sin((strip + .5f) / 16 * Mathf.PI));
                        Quad(x0, x1, altitude, altitude, altitude + height, altitude + height, tint, edge);
                    }
                }
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }

        private void Quad(float left, float right, float bottomLeft, float bottomRight, float topLeft, float topRight, Color bottom, Color top)
        {
            int i = vertices.Count;
            vertices.Add(new Vector3(left, bottomLeft, 1)); vertices.Add(new Vector3(right, bottomRight, 1));
            vertices.Add(new Vector3(right, topRight, 1)); vertices.Add(new Vector3(left, topLeft, 1));
            colors.Add(bottom); colors.Add(bottom); colors.Add(top); colors.Add(top);
            triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1); triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2);
        }
        private static float Snap(float value) => Mathf.Round(value / Pixel) * Pixel;
        private static Color Linear(Color value) => QualitySettings.activeColorSpace == ColorSpace.Linear ? value.linear : value;
        public void Dispose()
        {
            foreach (var asset in new UnityEngine.Object[] { root, mesh })
                if (Application.isPlaying) UnityEngine.Object.Destroy(asset); else UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}
