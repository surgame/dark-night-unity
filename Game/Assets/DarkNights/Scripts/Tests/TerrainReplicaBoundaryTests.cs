using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Networking;
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
            using var session = CreateWorkshop();
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
                for (int hit = 0; hit < 32 && !session.Map.Read(center).Cell.IsEmpty; hit++)
                    session.Map.DestroyTrusted(1, "edge:" + row + ":" + hit, TerrainEditAction.Explosive, session.Map.World,
                        center, session.Map.BuildTargets(TerrainEditAction.Explosive, center), _ => true);
                Assert.That(session.Map.Read(center).Cell.IsEmpty, Is.True, "边界变化检查仍须经过完整格耐久。");
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

        [Test]
        public void ProjectionRefreshSurvivesHelloAndUsesTheLatestCompleteRegion()
        {
            using var session = CreateWorkshop();
            var map = session.Map;
            var gameplay = map.Rules.Business.Gameplay;
            string visual = new string('c', 64);
            var replica = TerrainMapNetworking.CreateReplica(gameplay, visual, map.Rules.Business);
            using var stream = TerrainMapNetworking.OpenStream(map,
                TerrainMapNetworking.Handshake(map, gameplay, visual), 7, _ => true, () => 1);
            void Flush()
            {
                byte[] packet;
                while ((packet = stream.Dequeue()) != null) replica.ReceivePacket(packet);
                stream.Acknowledge(replica.CommitId);
            }
            var first = new GridBounds(0, -96, 96, 97);
            var next = new GridBounds(192, -96, 96, 97);
            stream.Subscribe(MineralRegionReadiness.Subscription(first, map.Descriptor.Bounds)); Flush();
            bool exclude = false;
            var target = new CellCoord(200, -50);
            var source = new TerrainReplicaSource(replica, (position, cell) => exclude && position.Equals(target) ? default : cell);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Action Bind(string name) => (Action)Delegate.CreateDelegate(typeof(Action), source, typeof(TerrainReplicaSource).GetMethod(name, flags));
            var request = Bind("RequestProjectionRefresh");
            var refresh = Bind("TryRefreshProjection");
            bool Pending() => (bool)typeof(TerrainReplicaSource).GetProperty("ProjectionRefreshPending", flags).GetValue(source);
            source.LoadAsync(replica.Descriptor, new ChunkCoord(0, -2), default).GetAwaiter().GetResult();
            var batches = new List<MapInputBatch>(); source.InputChanged += batches.Add;
            source.PublishInitialBaseline();
            replica.Applied += source.NotifyChanged;
            stream.Subscribe(MineralRegionReadiness.Subscription(next, map.Descriptor.Bounds));
            Assert.That(replica.ReceivePacket(stream.Dequeue()).Kind, Is.EqualTo(MapMessage.Hello));
            Assert.That(replica.Read(target).State, Is.EqualTo(GridSampleState.Unknown));
            exclude = true; request(); request();
            int count = batches.Count;
            Assert.DoesNotThrow(() => { refresh(); refresh(); });
            Assert.That(Pending(), Is.True);
            Assert.That(batches.Count, Is.EqualTo(count), "基线未齐时不能发布空白投影。");
            Flush();
            Assert.That(MineralRegionReadiness.Complete(replica, replica.Descriptor, next), Is.True);
            Assert.That(Pending(), Is.True, "旧跟踪区不属于新区域，不能提前消费请求。");
            typeof(TerrainReplicaSource).GetMethod("PrepareRegion", flags).Invoke(source, new object[] { next });
            source.LoadAsync(replica.Descriptor, new ChunkCoord(6, -2), default).GetAwaiter().GetResult();
            source.PublishInitialBaseline();
            var projected = batches[batches.Count - 1];
            Assert.That(Pending(), Is.False, "正常换区基线可以一并消费最新投影。");
            Assert.That(projected.StreamGeneration, Is.EqualTo(replica.Generation));
            Assert.That(projected.SnapshotChunks.Count, Is.EqualTo(1));
            Assert.That(projected.SnapshotChunks[0].Coordinate, Is.EqualTo(new ChunkCoord(6, -2)));
            Assert.That(projected.SnapshotChunks[0].Cells[14 * 32 + 8].IsEmpty, Is.True);
            Assert.That(replica.Read(target).Cell.IsEmpty, Is.False, "表现投影不得改变网络副本。");
            exclude = false; request(); refresh();
            var restored = batches[batches.Count - 1];
            Assert.That(restored.SourceCommit, Is.EqualTo(projected.SourceCommit));
            Assert.That(restored.InputGeneration, Is.EqualTo(projected.InputGeneration));
            Assert.That(restored.SnapshotChunks[0].Cells[14 * 32 + 8].IsEmpty, Is.False);
            count = batches.Count; refresh(); Assert.That(batches.Count, Is.EqualTo(count));
            request(); replica.Disconnect(); count = batches.Count; refresh();
            Assert.That(batches.Count, Is.EqualTo(count), "断线后不能重新发布待办投影。");
            Assert.That(replica.LastObserverError, Is.Null);
            replica.Applied -= source.NotifyChanged;
        }

        private static CaveWorkshopSession CreateWorkshop()
        {
            var cells = new byte[320 * 192]; for (int i = 0; i < cells.Length; i++) cells[i] = 2;
            var blueprint = new TerrainBlueprint(new TerrainGenerationSettings(), cells, new bool[cells.Length],
                new int[320], new[] { new TerrainRoom("gallery", 40, 45, 10, 10) });
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>("Assets/DarkNights/Res/Terrain/StrataCave/MapDefinition.asset");
            var game = Runtime.Config.GameCatalogJson.Parse(
                AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>("Assets/DarkNights/Res/Config/balance.json").text,
                AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>("Assets/DarkNights/Res/Config/pinewatch.json").text);
            return new CaveWorkshopSession(blueprint, definition.LoadGameplayCatalog(), game);
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
