using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>混合碎岩落地后的原生像素网格；与飞行碎岩共用冻结大小、姿态和画法，保留颗粒间隙。只按权威清障区域裁切，没有另一套耐久或碰撞，销毁时释放实例网格。</summary>
    public sealed class GeologyPileView : MonoBehaviour
    {
        public MeshFilter Geometry;
        public MeshRenderer Surface;
        private TerrainCellShape[] shapes;
        private int[] left, bottom;
        private int width, height;
        private Mesh mesh;
        private RubbleFragmentViewData[] pieces;
        private RubbleCollapseViewData collapse;
        private int originX, originY;
        private Color32[] pixels;
        private uint previousMask = uint.MaxValue;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        public void Bind(FallingRockViewData[] motions, TerrainCellShape[] cellShapes, RubbleCollapseViewData frozen)
        {
            if (Geometry == null || Surface == null || Surface.sharedMaterial == null || motions == null ||
                cellShapes == null || cellShapes.Length != motions.Length || motions.Length == 0 || motions.Length > 31)
                throw new InvalidOperationException("土堆缺少明确的网格、土纹、落点或坡形绑定。");
            shapes = (TerrainCellShape[])cellShapes.Clone();
            left = new int[motions.Length]; bottom = new int[motions.Length];
            originX = int.MaxValue; originY = int.MaxValue;
            foreach (var motion in motions)
            { originX = Math.Min(originX, Mathf.RoundToInt(motion.LandingX) - 8); originY = Math.Min(originY, Mathf.RoundToInt(motion.LandingY) - 8); }
            width = height = 0;
            for (int i = 0; i < motions.Length; i++)
            {
                left[i] = Mathf.RoundToInt(motions[i].LandingX) - 8 - originX;
                bottom[i] = Mathf.RoundToInt(motions[i].LandingY) - 8 - originY;
                width = Math.Max(width, left[i] + 16); height = Math.Max(height, bottom[i] + 16);
            }
            if (width > 256 || height > 96) throw new InvalidOperationException("土堆表现超出有界像素范围。");
            transform.position = new Vector3(originX / 100f, originY / 100f, 0);
            collapse = frozen ?? throw new ArgumentNullException(nameof(frozen));
            pieces = new RubbleFragmentViewData[collapse.Count];
            for (int i = 0; i < pieces.Length; i++) pieces[i] = collapse.Fragment(i);
            pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) pixels[y * width + x] = Pixel(x, y);
            if (mesh == null) { mesh = new Mesh { name = "Geology mixed rock instance" }; mesh.MarkDynamic(); }
            Geometry.sharedMesh = mesh; previousMask = uint.MaxValue;
            Present(0);
        }

        public void Present(uint occupied)
        {
            if (mesh == null || previousMask == occupied) return;
            previousMask = occupied; vertices.Clear(); uv.Clear(); colors.Clear(); triangles.Clear();
            Surface.enabled = occupied != 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width;)
                {
                    if (!Filled(x, y, occupied) || pixels[y * width + x].a == 0) { x++; continue; }
                    int start = x; Color32 tone = pixels[y * width + x]; x++;
                    while (x < width && Filled(x, y, occupied) && pixels[y * width + x].Equals(tone)) x++;
                    Strip(start, x, y, tone);
                }
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }

        private bool Filled(int x, int y, uint occupied)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return false;
            for (int i = 0; i < left.Length; i++)
            {
                if ((occupied & 1u << i) == 0) continue;
                int localX = x - left[i], localY = y - bottom[i];
                if (localX >= 0 && localX < 16 && localY >= 0 && localY < 16 &&
                    TerrainShapeGeometry.Contains(shapes[i], (localX + .5f) / 16, (localY + .5f) / 16)) return true;
            }
            return false;
        }

        private Color32 Pixel(int x, int y)
        {
            Color32 color = default;
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                collapse.Sample(i, collapse.Duration, out float px, out float py, out float angle);
                float dx = x + .5f + originX - px, dy = y + .5f + originY - py;
                float radians = angle * Mathf.Deg2Rad, cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
                int u = Mathf.FloorToInt((dx * cos + dy * sin) / piece.Width * 16 + 8);
                int v = Mathf.FloorToInt((-dx * sin + dy * cos) / piece.Height * 16 + 8);
                var sample = GeologyRockPixels.Sample(piece.Variant, u, v);
                if (sample.a != 0) color = sample;
            }
            return color;
        }

        private void Strip(int start, int end, int y, Color32 tone)
        {
            int offset = vertices.Count;
            vertices.Add(new Vector3(start / 100f, y / 100f, 0));
            vertices.Add(new Vector3(start / 100f, (y + 1) / 100f, 0));
            vertices.Add(new Vector3(end / 100f, (y + 1) / 100f, 0));
            vertices.Add(new Vector3(end / 100f, y / 100f, 0));
            for (int i = 0; i < 4; i++) uv.Add(Vector2.zero);
            Color color = ((Color)tone).linear;
            for (int i = 0; i < 4; i++) colors.Add(color);
            triangles.Add(offset); triangles.Add(offset + 1); triangles.Add(offset + 2);
            triangles.Add(offset); triangles.Add(offset + 2); triangles.Add(offset + 3);
        }

        private void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }
    }
}
