using System;
using System.Collections.Generic;
using System.Threading;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;

namespace DarkNights.Tests
{
    /// <summary>验证工作台冻结快照在世界边缘的变化局部性；未改动基线不应改变区块，外部 padding 不计作世界内容。</summary>
    public sealed class TerrainReplicaBoundaryTests
    {
        [Test]
        public void AuthorityPaddingIsStableAndEdgeEditsRemainLocal()
        {
            var cells = new byte[320 * 192]; for (int i = 0; i < cells.Length; i++) cells[i] = 2;
            var blueprint = new TerrainBlueprint(new TerrainGenerationSettings(), cells, new bool[cells.Length],
                new int[320], new[] { new TerrainRoom("gallery", 40, 45, 10, 10) });
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>("Assets/DarkNights/Res/Terrain/StrataCave/MapDefinition.asset");
            var game = Runtime.Config.GameCatalogJson.Parse(
                AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>("Assets/DarkNights/Res/Config/balance.json").text,
                AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>("Assets/DarkNights/Res/Config/pinewatch.json").text);
            using var session = new CaveWorkshopSession(blueprint, definition.LoadGameplayCatalog(), game);
            var source = new TerrainReplicaSource(session.Map);
            for (int v = -6; v <= 0; v++) for (int u = 0; u < 10; u++)
                source.LoadAsync(session.Map.Descriptor, new ChunkCoord(u, v), CancellationToken.None).GetAwaiter().GetResult();
            MapInputBatch current = null; source.InputChanged += batch => current = batch;
            source.PublishInitialBaseline(); Assert.That(current, Is.Not.Null);
            Assert.That(current.Kind, Is.EqualTo(MapInputBatchKind.Baseline));
            MapInputBatch previous = current;
            current = null; source.NotifyChanged(); Assert.That(current, Is.Not.Null);
            Assert.That(ChangedChunks(previous, current), Is.Empty, "未编辑的边界不能误报。");
            previous = current;
            foreach (int row in new[] { 0, 191 })
            {
                var center = new CellCoord(15, -row);
                session.Map.DestroyTrusted(1, "edge:" + row, TerrainEditAction.Explosive, session.Map.World,
                    center, session.Map.BuildTargets(TerrainEditAction.Explosive, center), _ => true);
                current = null; source.NotifyChanged(); Assert.That(current, Is.Not.Null);
                var changed = ChangedChunks(previous, current);
                Assert.That(changed.Count, Is.EqualTo(row == 0 ? 2 : 1));
                Assert.That(changed, Does.Contain(new ChunkCoord(0, GridMath.FloorDiv(-row, 32))));
                if (row == 0) Assert.That(changed, Does.Contain(new ChunkCoord(0, -1)));
                previous = current;
                current = null; source.NotifyChanged(); Assert.That(current, Is.Not.Null);
                Assert.That(ChangedChunks(previous, current), Is.Empty);
                previous = current;
            }
        }

        private static IReadOnlyList<ChunkCoord> ChangedChunks(MapInputBatch previous, MapInputBatch current)
        {
            var remaining = new Dictionary<ChunkCoord, IReadOnlyList<GridCell>>();
            foreach (var snapshot in previous.SnapshotChunks) remaining[snapshot.Coordinate] = snapshot.Cells;
            var changed = new List<ChunkCoord>();
            foreach (var snapshot in current.SnapshotChunks)
            {
                if (!remaining.TryGetValue(snapshot.Coordinate, out var cells) || !SameCells(cells, snapshot.Cells))
                    changed.Add(snapshot.Coordinate);
                remaining.Remove(snapshot.Coordinate);
            }
            changed.AddRange(remaining.Keys);
            return changed;
        }

        private static bool SameCells(IReadOnlyList<GridCell> left, IReadOnlyList<GridCell> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (left[i].TileId != right[i].TileId || left[i].Height != right[i].Height || left[i].Flags != right[i].Flags)
                    return false;
            return true;
        }
    }
}
