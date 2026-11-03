using System;
using System.Collections.Generic;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.Physics;
using DarkNights.Runtime.Terrain;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 权威格形到原生碰撞的派生器；整格按行合并，坡形按原12种公式落入多边形。
    /// 地图或revision变化时在模拟前统一重建，不修改作者格子，不以视觉网格推断碰撞。
    /// </summary>
    internal sealed class NativePhysicsTerrain : IDisposable
    {
        private readonly GameObject root;
        private readonly PolygonCollider2D polygons;
        private readonly CompositeCollider2D composite;
        private readonly PhysicsMaterial2D material;
        private TerrainMapAuthority source;
        private ulong revision = ulong.MaxValue;
        private bool flatBuilt;

        internal NativePhysicsTerrain(Scene scene, float friction)
        {
            root = new GameObject("Authority terrain collision") { hideFlags = HideFlags.HideInHierarchy };
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            polygons = root.AddComponent<PolygonCollider2D>();
            composite = root.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Manual;
            composite.vertexDistance = .0001f;
            material = new PhysicsMaterial2D("Authority terrain") { friction = friction, bounciness = 0 };
            polygons.sharedMaterial = material; composite.sharedMaterial = material;
            polygons.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        internal void Synchronize(TerrainMapAuthority map, LevelLayout layout, float maximumHeight)
        {
            if (map == null && flatBuilt || map != null && source == map && revision == map.CommitId) return;
            var paths = new List<Vector2[]>();
            if (map == null)
            {
                if (layout.Platforms.Count != 0) throw new InvalidOperationException("物理候选不接受历史单向平台布局。");
                Rectangle(paths, 0, -128, layout.WorldWidth, 0);
                flatBuilt = true;
            }
            else
            {
                for (int row = 0; row < TerrainGenerationSettings.Height; row++)
                {
                    int run = -1;
                    for (int column = 0; column <= TerrainGenerationSettings.Width; column++)
                    {
                        bool solid = false; TerrainCellShape shape = TerrainCellShape.Full;
                        if (column < TerrainGenerationSettings.Width)
                        {
                            var sample = map.Read(new CellCoord(column, -row));
                            solid = !sample.TryGetCell(out var cell) || !cell.IsEmpty;
                            if (sample.TryGetCell(out cell)) shape = TerrainShapeGeometry.Decode(cell.Flags);
                        }
                        bool full = solid && shape == TerrainCellShape.Full;
                        if (full) { if (run < 0) run = column; continue; }
                        float y = PlayableTerrain.OriginY - row * PlayableTerrain.CellPixels;
                        if (run >= 0)
                        {
                            Rectangle(paths, (run - .5f) * 16, y - 8, (column - .5f) * 16, y + 8);
                            run = -1;
                        }
                        if (solid) Slope(paths, shape, column * 16 - 8, y - 8);
                    }
                }
                source = map; revision = map.CommitId;
            }
            Rectangle(paths, -32, -4096, 8, 8192);
            Rectangle(paths, layout.WorldWidth - 8, -4096, layout.WorldWidth + 32, 8192);
            float bottom = PlayableTerrain.MinimumHeight;
            Rectangle(paths, -32, bottom - 64, layout.WorldWidth + 32, bottom);
            float top = map == null ? maximumHeight + 44 : PlayableTerrain.OriginY + 8;
            Rectangle(paths, -32, top, layout.WorldWidth + 32, top + 64);
            polygons.pathCount = paths.Count;
            for (int i = 0; i < paths.Count; i++) polygons.SetPath(i, paths[i]);
            composite.GenerateGeometry();
            Physics2D.SyncTransforms();
        }

        private static void Rectangle(List<Vector2[]> paths, float x0, float y0, float x1, float y1) =>
            paths.Add(new[] { Point(x0, y0), Point(x1, y0), Point(x1, y1), Point(x0, y1) });

        private static void Slope(List<Vector2[]> paths, TerrainCellShape shape, float x, float y)
        {
            float left = TerrainShapeGeometry.Edge(shape, 0), right = TerrainShapeGeometry.Edge(shape, 1);
            var points = new List<Vector2>();
            if (TerrainShapeGeometry.Ceiling(shape))
                points.AddRange(new[] { Point(x, y + left * 16), Point(x + 16, y + right * 16), Point(x + 16, y + 16), Point(x, y + 16) });
            else points.AddRange(new[] { Point(x, y), Point(x + 16, y), Point(x + 16, y + right * 16), Point(x, y + left * 16) });
            for (int i = points.Count - 1; i >= 0; i--)
                if (points[i] == points[(i + 1) % points.Count]) points.RemoveAt(i);
            if (points.Count >= 3) paths.Add(points.ToArray());
        }

        private static Vector2 Point(float x, float y) => new Vector2(x, y) / PhysicsNumbers.PixelsPerUnit;
        public void Dispose()
        {
            NativePhysicsBodies.Retire(root); NativePhysicsBodies.Retire(material);
        }
    }
}
