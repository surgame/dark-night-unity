using System;
using System.Linq;
using System.Threading;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config.Terrain;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>局部装载的岩壁缓存回归；比较分次装载与初次装载的相同冻结输入，不改作者资产或权威地图。</summary>
    public sealed class TerrainStreamingCacheTests
    {
        [TestCase(4, -3)]
        [TestCase(0, -5)]
        public void LaterChunksHaveTheSameRockGeometryAsInitialChunks(int u, int v)
        {
            const string root = "Assets/DarkNights/Res/Terrain/StrataCave/";
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(root + "MapDefinition.asset");
            var style = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(root + "Style.asset"));
            style.ImmediateForeground = true;
            var map = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(root + "ReferenceChamber.asset");
            var blueprint = map.ReadBlueprint();
            var identity = new WorldIdentity(StableGuid.Parse(Guid.NewGuid().ToString("N")), 1);
            var descriptor = definition.Describe(identity, 0);
            var catalog = definition.LoadGameplayCatalog().Tiles;
            var reference = new BackgroundBakeDescriptor(identity.WorldId.ToString(), blueprint.Settings.Seed,
                blueprint.CopyMaterials(), blueprint.CopyShapes());
            var parent = new GameObject("Streaming cache regression");
            CaveVisualSource incremental = null, initial = null;
            try
            {
                incremental = new CaveVisualSource(new TerrainBlueprintSource(blueprint, catalog), style, catalog, parent.transform, reference);
                initial = new CaveVisualSource(new TerrainBlueprintSource(blueprint, catalog), style, catalog, parent.transform, reference);
                var first = new ChunkCoord(0, -3); var next = new ChunkCoord(u, v);
                incremental.LoadAsync(descriptor, first, CancellationToken.None).GetAwaiter().GetResult();
                incremental.Flush();
                var added = incremental.LoadAsync(descriptor, next, CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(added.Cells.Any(c => !c.IsEmpty), Is.True, "后加载的夹具区块必须包含实心岩格。");
                incremental.Flush();
                initial.LoadAsync(descriptor, first, CancellationToken.None).GetAwaiter().GetResult();
                initial.LoadAsync(descriptor, next, CancellationToken.None).GetAwaiter().GetResult();
                initial.Flush();
                var a = Geometry(incremental); var b = Geometry(initial);
                CollectionAssert.AreEqual(TerrainVisualTestScope.Read<byte[]>(b, "materials"), TerrainVisualTestScope.Read<byte[]>(a, "materials"));
                CollectionAssert.AreEqual(TerrainVisualTestScope.Read<byte[]>(b, "shapes"), TerrainVisualTestScope.Read<byte[]>(a, "shapes"));
                var pixels = TerrainVisualTestScope.Read<Color32[]>(incremental, "cells");
                Assert.That(pixels.Any(c => c.r != 0), Is.True, "夹具必须包含实心岩格。");
                CollectionAssert.AreEqual(pixels.Select(c => c.r).ToArray(), TerrainVisualTestScope.Read<byte[]>(a, "materials"));
            }
            finally
            {
                incremental?.Dispose(); initial?.Dispose();
                UnityEngine.Object.DestroyImmediate(parent); UnityEngine.Object.DestroyImmediate(style);
            }
        }

        private static object Geometry(CaveVisualSource source) => TerrainVisualTestScope.Read<object>(
            TerrainVisualTestScope.Read<object>(source, "localRockSurface"), "geometry");
    }
}
