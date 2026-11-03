using System;
using System.Collections.Generic;
using System.IO;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Entry.Terrain;
using DarkNights.View;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 显式烘焙入口白模的原生网格和边线，来源为Prefab内的轮廓，网格与运行格形状共用同一冻结数据。
    /// 只写指定入口白模资源目录，保留已有资产GUID；不在打开场景、导入或Play时重建作者资源。
    /// 编辑实例前须进入Prefab模式，避免实例轮廓单独覆盖却改动所有实例共享网格。
    /// </summary>
    public static class WhiteboxModuleBaker
    {
        public const string AssetRoot = TerrainScenePaths.Workbenches + "/EntranceWhiteboxAssets";

        [DarkNightsWorkbenchCommand("Dark Nights/Terrain/重建选中入口白模", "重建选中入口白模")]
        private static void RebuildSelected()
        {
            var selected = Selection.activeGameObject;
            var source = selected == null ? null : selected.GetComponentInParent<WhiteboxTerrainAuthoring>();
            if (source == null) throw new InvalidOperationException("先在Prefab模式选择入口模块根节点。");
            if (PrefabUtility.IsPartOfPrefabInstance(source.gameObject) &&
                PrefabStageUtility.GetPrefabStage(source.gameObject) == null)
                throw new InvalidOperationException("请先打开该入口Prefab，修改轮廓后再重建；不自动应用场景实例覆盖。");
            string meshPath = AssetDatabase.GetAssetPath(source.TerrainMesh.sharedMesh);
            Bake(source, Path.GetDirectoryName(meshPath).Replace("\\", "/"));
        }

        public static void Bake(WhiteboxTerrainAuthoring source, string directory)
        {
            if (string.IsNullOrEmpty(directory) || !directory.StartsWith(AssetRoot + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("白模输出只能位于专用入口目录。");
            if (source.TerrainMesh == null || source.BackgroundMesh == null ||
                source.FloorLine == null || source.CeilingLine == null)
                throw new InvalidOperationException("入口模块缺少显式网格和边线绑定。");
            PlayableTerrain data = WhiteboxTerrainBuilder.Build(source);
            if (source.TerrainArtwork != null &&
                source.ArtworkFingerprint != WhiteboxTerrainBuilder.Fingerprint(source))
                throw new InvalidOperationException("请先按新轮廓重导SVG图层，不允许显示图与地形分离。");
            byte[] shapes = data.CopyShapes();
            var body = new List<Vector3>();
            var triangles = new List<int>();
            for (int row = 0; row < TerrainGenerationSettings.Height; row++)
                for (int u = 0; u * .16f < source.Width; u++)
                {
                    float bottom = (PlayableTerrain.OriginY - (row + .5f) * 16) / EntityView.PixelsPerUnit;
                    if (bottom < source.DisplayBottom - .001f || bottom + .16f > source.DisplayTop + .001f ||
                        data.Material(u, row) == 0) continue;
                    var shape = (TerrainCellShape)shapes[row * TerrainGenerationSettings.Width + u];
                    Vector2[] polygon = Shape(shape);
                    int start = body.Count;
                    foreach (Vector2 p in polygon) body.Add(new Vector3((u - .5f + p.x) * .16f, bottom + p.y * .16f));
                    Fan(triangles, start, polygon.Length);
                }
            WriteMesh(source.TerrainMesh, directory + "/Terrain.asset", body, triangles);
            body.Clear(); triangles.Clear();
            float from = source.Ceiling[0].x;
            for (int step = 0; step < 256; step++)
            {
                float left = Mathf.Lerp(from, source.Width, step / 256f);
                float right = Mathf.Lerp(from, source.Width, (step + 1) / 256f);
                int start = body.Count;
                body.Add(new Vector3(left, WhiteboxTerrainAuthoring.Height(source.Floor, left), 1));
                body.Add(new Vector3(right, WhiteboxTerrainAuthoring.Height(source.Floor, right), 1));
                body.Add(new Vector3(right, WhiteboxTerrainAuthoring.Height(source.Ceiling, right), 1));
                body.Add(new Vector3(left, WhiteboxTerrainAuthoring.Height(source.Ceiling, left), 1));
                Fan(triangles, start, 4);
            }
            WriteMesh(source.BackgroundMesh, directory + "/Background.asset", body, triangles);
            WriteLine(source.FloorLine, source.Floor);
            WriteLine(source.CeilingLine, source.Ceiling);
            Undo.RecordObject(source, "烘焙入口白模");
            source.BakedFingerprint = WhiteboxTerrainBuilder.Fingerprint(source);
            EditorUtility.SetDirty(source);
        }

        private static Vector2[] Shape(TerrainCellShape shape)
        {
            float left = TerrainShapeGeometry.Edge(shape, 0), right = TerrainShapeGeometry.Edge(shape, 1);
            if (TerrainShapeGeometry.Ceiling(shape))
                return new[] { new Vector2(0, left), new Vector2(1, right), Vector2.one, new Vector2(0, 1) };
            return new[] { Vector2.zero, new Vector2(1, 0), new Vector2(1, right), new Vector2(0, left) };
        }

        private static void Fan(List<int> triangles, int start, int count)
        {
            for (int i = 1; i < count - 1; i++)
            { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
        }

        private static void WriteMesh(MeshFilter target, string path, List<Vector3> vertices, List<int> triangles)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mesh, path);
            }
            else Undo.RecordObject(mesh, "重建入口网格");
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.uv = new Vector2[vertices.Count];
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Undo.RecordObject(target, "绑定入口网格");
            target.sharedMesh = mesh;
            EditorUtility.SetDirty(mesh);
            EditorUtility.SetDirty(target);
        }

        private static void WriteLine(LineRenderer line, Vector2[] points)
        {
            Undo.RecordObject(line, "重建入口边线");
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, new Vector3(points[i].x, points[i].y, -.02f));
            EditorUtility.SetDirty(line);
        }
    }
}
