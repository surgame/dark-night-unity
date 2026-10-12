using System;
using System.Collections;
using System.Threading;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using DarkNights.Core.Logic.Lighting;
using DarkNights.Core.ViewData;
using DarkNights.View.Lighting;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>真实表现地形上的缓存票据回归；模拟迟到任务、换区、挖墙版本与退休，不创建网络会话或访问玩家存档。</summary>
    public sealed class WallDiffuseCacheTests
    {
        [UnityTest]
        public IEnumerator StaticGeometryIsReusedAndLateBoundsResultIsDiscarded()
        {
            const string root = "Assets/DarkNights/Res/Terrain/StrataCave/";
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(root + "MapDefinition.asset");
            var style = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(root + "Style.asset"));
            style.ProceduralRock = false;
            var map = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(root + "ReferenceChamber.asset");
            var blueprint = map.ReadBlueprint();
            var descriptor = definition.Describe(new WorldIdentity(StableGuid.Parse(Guid.NewGuid().ToString("N")), 1), 0);
            var parent = new GameObject("Diffuse cache test");
            var source = new CaveVisualSource(new TerrainBlueprintSource(blueprint, definition.LoadGameplayCatalog().Tiles), style,
                definition.LoadGameplayCatalog().Tiles, parent.transform, new DarkNights.Core.Config.Terrain.BackgroundBakeDescriptor(
                    descriptor.World.WorldId.ToString(), blueprint.Settings.Seed, blueprint.CopyMaterials(), blueprint.CopyShapes()));
            using (var gate = new ManualResetEventSlim(false))
            {
                var cache = new LightBoundaryCache((frozen, cancelled) => { gate.Wait(); return LightBoundaryBuilder.Build(frozen, cancelled); });
                try
                {
                    source.LoadAsync(descriptor, new ChunkCoord(0, -1), CancellationToken.None).GetAwaiter().GetResult();
                    var first = new RectInt(0, 0, 32, 32); var next = new RectInt(0, 32, 32, 32);
                    cache.Tick(source, first); cache.Tick(source, next); Assert.That(cache.BuildCount, Is.EqualTo(1));
                    gate.Set(); double deadline = EditorApplication.timeSinceStartup + 10;
                    while (cache.Snapshot == null && EditorApplication.timeSinceStartup < deadline) { cache.Tick(source, next); yield return null; }
                    Assert.That(cache.DiscardCount, Is.EqualTo(1)); Assert.That(cache.Bounds, Is.EqualTo(next));
                    var installed = cache.Snapshot; int count = cache.BuildCount;
                    for (int i = 0; i < 100; i++) cache.Tick(source, next);
                    Assert.That(cache.Snapshot, Is.SameAs(installed)); Assert.That(cache.BuildCount, Is.EqualTo(count));
                    gate.Reset(); source.LoadAsync(descriptor, new ChunkCoord(1, -1), CancellationToken.None).GetAwaiter().GetResult();
                    cache.Tick(source, next);
                    Assert.That(cache.Snapshot, Is.SameAs(installed), "同源版本变化期间保留有效墙段，GPU 用当前格逐点拒绝旧边。");
                    source.Dispose(); cache.Tick(source, next); Assert.That(cache.Snapshot, Is.Null);
                    gate.Set(); var retirement = cache.RetireAsync(); while (!retirement.IsCompleted) yield return null;
                    retirement.GetAwaiter().GetResult(); Assert.That(cache.Snapshot, Is.Null);
                }
                finally
                {
                    gate.Set(); cache.RetireAsync().GetAwaiter().GetResult(); source.Dispose();
                    UnityEngine.Object.DestroyImmediate(parent); UnityEngine.Object.DestroyImmediate(style);
                }
            }
        }
    }
}
