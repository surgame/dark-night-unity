using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using UnityEngine;

namespace DarkNights.View.Expedition
{
    /// <summary>同贴图及排序层的可见装饰网格；复用顶点缓冲，材质沿现有Camp Sprite受光，退休时释放本批资源。</summary>
    internal sealed class SurfaceSceneryBatch : IDisposable
    {
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly Texture2D texture;
        private readonly List<Vector3> vertices = new List<Vector3>(1024);
        private readonly List<Vector2> uvs = new List<Vector2>(1024);
        private readonly List<Color> colors = new List<Color>(1024);
        private readonly List<int> triangles = new List<int>(1536);

        internal SurfaceSceneryBatch(Transform parent, Texture2D texture, int order, Material template)
        {
            this.texture = texture;
            root = new GameObject("Surface decoration batch " + texture.name); root.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Visible supported surface decoration" }; mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sortingOrder = order;
            material = new Material(template) { mainTexture = texture }; renderer.sharedMaterial = material;
        }

        internal void Clear() { vertices.Clear(); uvs.Clear(); colors.Clear(); triangles.Clear(); }
        internal void Add(SurfaceSceneryPlacement p)
        {
            float width = p.Pixels.width / 16f * p.Scale, height = p.Pixels.height / 16f * p.Scale;
            float bottom = .5f - p.Row - (1 - TerrainShapeGeometry.Edge((TerrainCellShape)p.Shape, .5f)) - .0625f;
            bottom += p.GroundOffsetPixels / 8;
            float left = p.Column - width * p.Pivot.x;
            bottom -= height * p.Pivot.y;
            var shape = (TerrainCellShape)p.Shape;
            float b0 = p.Conform ? bottom + TerrainShapeGeometry.Edge(shape, Mathf.Clamp01(.5f - width * p.Pivot.x)) -
                TerrainShapeGeometry.Edge(shape, .5f) : bottom;
            float b1 = p.Conform ? bottom + TerrainShapeGeometry.Edge(shape, Mathf.Clamp01(.5f + width * (1 - p.Pivot.x))) -
                TerrainShapeGeometry.Edge(shape, .5f) : bottom;
            float u0 = p.Pixels.x / (float)texture.width, u1 = p.Pixels.xMax / (float)texture.width;
            if (p.Flip) (u0, u1) = (u1, u0);
            float v0 = p.Pixels.y / (float)texture.height, v1 = p.Pixels.yMax / (float)texture.height;
            int i = vertices.Count;
            vertices.Add(new Vector3(left, b0, 0)); vertices.Add(new Vector3(left + width, b1, 0));
            vertices.Add(new Vector3(left + width, b1 + height, 0)); vertices.Add(new Vector3(left, b0 + height, 0));
            uvs.Add(new Vector2(u0, v0)); uvs.Add(new Vector2(u1, v0));
            uvs.Add(new Vector2(u1, v1)); uvs.Add(new Vector2(u0, v1));
            for (int c = 0; c < 4; c++) colors.Add(Color.white);
            triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
            triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2);
        }
        internal void Upload()
        {
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            root.SetActive(vertices.Count != 0);
        }
        public void Dispose()
        {
            foreach (var asset in new UnityEngine.Object[] { root, mesh, material })
                if (Application.isPlaying) UnityEngine.Object.Destroy(asset); else UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}
