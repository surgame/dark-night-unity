using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.MapModule;
using DarkNights.Core.Logic.MapModule;
using DarkNights.View.MapModule;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.MapModule
{
    /// <summary>
    /// 首批模块原生Prefab及轮廓预览的有限生产器；仅写尚不存在的目标资产，不替换人工Prefab或旧版本网格。
    /// 作者轮廓始终来自MapModuleAuthoring，预览网格为其可追溯派生，运行碰撞另由精确格形编译。
    /// </summary>
    internal static class MapModulePrefabFactory
    {
        internal static GameObject Create(string id, string title, int[][] floor, int[][] roof,
            MapModulePort[] ports, Material material)
        {
            string path = MapModulePaths.Modules + "/" + id + ".prefab";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("初始化不覆盖已有模块：" + path);
            var root = new GameObject(title);
            try
            {
                var author = root.AddComponent<MapModuleAuthoring>();
                author.ModuleId = id; author.DisplayName = title; author.Footprint = new Vector2Int(4, 3);
                author.Solids = new[] { Solid("ground", floor), Solid("ceiling", roof) }; author.Ports = ports;
                var mesh = BuildPreview(author);
                string meshPath = MapModulePaths.Modules + "/" + id + "-initial-mesh.asset";
                if (AssetDatabase.LoadMainAssetAtPath(meshPath) != null) throw new InvalidOperationException("预览资产已存在。");
                AssetDatabase.CreateAsset(mesh, meshPath);
                var visual = new GameObject("Contour preview"); visual.transform.SetParent(root.transform, false);
                visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                author.PreviewRenderer = visual.AddComponent<MeshRenderer>();
                author.PreviewRenderer.sharedMaterial = material; author.PreviewRenderer.sortingOrder = 0;
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static MapModuleSolid Solid(string id, int[][] vertices) => new MapModuleSolid
        {
            Id = id, Material = 2, Vertices = vertices.Select(p => new Vector2Int(p[0], p[1])).ToArray()
        };
        internal static Mesh BuildPreview(MapModuleAuthoring author)
        {
            var vertices = new List<Vector3>(); var indices = new List<int>(); var colors = new List<Color32>();
            foreach (var solid in author.Solids)
                foreach (var triangle in MapModuleGeometry.Triangulate(solid.Vertices.Select(p => new MapModuleVertex(p.x, p.y)).ToArray()))
                {
                    int start = vertices.Count;
                    foreach (var point in triangle)
                    {
                        vertices.Add(new Vector3(((float)point.X - .5f) * .16f, (float)point.Y * .16f, 0));
                        colors.Add(new Color32(132, 143, 111, 255));
                    }
                    indices.Add(start); indices.Add(start + 2); indices.Add(start + 1);
                }
            var mesh = new Mesh { name = author.ModuleId + " contour preview" };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.SetColors(colors); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
