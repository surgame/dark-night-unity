using System;
using System.Collections.Generic;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Networking;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Editor.Terrain;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Runner;
using GameCore.Objects.Runner.DI;
using NUnit.Framework;
using UnityEditor;

namespace DarkNights.Tests
{
    /// <summary>地图独立权威、局部提交、静态网络零扫描、撤权与晚加入回归；不替代真实双进程联测。</summary>
    public sealed class TerrainAuthorityTests
    {
        private DIContainer container;
        private ObjectSessionContext session;
        private TerrainMapAuthority map;
        private ServerGameplayCatalog gameplay;
        private string visualDigest;
        private bool current;
        [SetUp]
        public void Setup()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(TerrainTestAssets.DefinitionPath);
            Assert.That(definition, Is.Not.Null);
            gameplay = definition.LoadGameplayCatalog(); visualDigest = new string('a', 64);
            container = new DIContainer(); container.Initialize(); current = true;
            session = ObjectSessionContext.CreateAuthority(container, () => current); session.Activate();
            map = new TerrainMapAuthority(session, TerrainGenerator.Generate(new TerrainGenerationSettings()), gameplay,
                new WorldIdentity(StableGuid.Parse("ac07eb5be9c646bf911002ed4c2bceef"), 1));
        }
        [TearDown]
        public void TearDown() { map?.Dispose(); session?.Dispose(); container?.OnReturnToPool(); }

        private CellCoord Target()
        {
            for (int v = -115; v <= -100; v++) for (int u = 115; u < 140; u++)
            {
                var p = new CellCoord(u, v);
                if (map.Read(p).TryGetCell(out var c) && !c.IsEmpty && c.Flags == 0) return p;
            }
            throw new Exception("Missing destructible fixture.");
        }
        private CellCoord SoftTarget()
        {
            for (int v = -115; v <= -100; v++) for (int u = 115; u < 140; u++)
            {
                var p = new CellCoord(u, v);
                if (map.IsSoftRock(p) && map.Read(p).TryGetCell(out var c) && !c.IsEmpty && c.Flags == 0) return p;
            }
            throw new Exception("Missing soft-rock fixture.");
        }
        [Test]
        public void LocalDestructionTouchesAtMostFourPagesAndIsFrozen()
        {
            var p = Target(); var before = map.CaptureSnapshot(new GridBounds(p.U, p.V, 1, 1));
            var receipt = map.DestroyTrusted(1, 1, map.World, map.CommitId, new[] { p }, _ => true);
            Assert.That(receipt.ChangedChunks.Count, Is.EqualTo(1));
            Assert.That(receipt.PageTargets.Count, Is.InRange(1, 4));
            Assert.That(before.Read(p).Cell.IsEmpty, Is.False);
            Assert.That(map.Read(p).Cell.IsEmpty, Is.True);
            Assert.Throws<InvalidOperationException>(() => map.DestroyTrusted(1, 1, map.World, map.CommitId, new[] { Target() }, _ => true));
        }
        [Test]
        public void InvalidBatchAndProtectedSupportAreAtomic()
        {
            var p = Target(); ulong before = map.CommitId;
            Assert.Throws<InvalidOperationException>(() => map.DestroyTrusted(1, 1, map.World, before,
                new[] { p, new CellCoord(62, -73) }, _ => true));
            Assert.That(map.CommitId, Is.EqualTo(before)); Assert.That(map.Read(p).Cell.IsEmpty, Is.False);
            Assert.Throws<InvalidOperationException>(() => map.DestroyTrusted(1, 1, map.World, before, new[] { p }, _ => false));
            current = false;
            Assert.Throws<InvalidOperationException>(() => map.DestroyTrusted(1, 1, map.World, before, new[] { p }, _ => true));
        }
        [Test]
        public void ActionPolicyAndRequestIdAreAuthoritativeAndIdempotent()
        {
            var soft = SoftTarget(); var normal = Target();
            Assert.That(map.BuildTargets(TerrainEditAction.HandMine, soft).Count, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => map.BuildTargets(TerrainEditAction.HandMine, normal));
            var targets = map.BuildTargets(TerrainEditAction.HandMine, soft);
            var first = map.DestroyTrusted(2, "mine-1", TerrainEditAction.HandMine, map.World, map.CommitId,
                soft, targets, _ => true);
            ulong after = map.CommitId;
            var retry = map.DestroyTrusted(2, "mine-1", TerrainEditAction.HandMine, map.World, 0, soft, targets, _ => false);
            Assert.That(retry, Is.SameAs(first)); Assert.That(map.CommitId, Is.EqualTo(after));
            Assert.Throws<InvalidOperationException>(() => map.DestroyTrusted(2, "mine-1", TerrainEditAction.HandMine,
                map.World, after, normal, new[] { normal }, _ => true));
            var blast = map.BuildTargets(TerrainEditAction.Explosive, normal);
            Assert.That(blast.Count, Is.InRange(1, 13));
        }
        [Test]
        public void IdleStreamDoesNotScanAndChangedMapReachesLateReplica()
        {
            var hello = TerrainMapNetworking.Handshake(map, gameplay, visualDigest);
            ulong policy = 1; bool permitted = true;
            var stream = TerrainMapNetworking.OpenStream(map, hello, 10, _ => permitted, () => policy);
            var replica = TerrainMapNetworking.CreateReplica(gameplay, visualDigest);
            stream.Subscribe(new GridBounds(96, -128, 64, 64)); Drain(stream, replica);
            long scans = stream.PublicationScanCount;
            for (int i = 0; i < 1000; i++) Assert.That(stream.Publish(), Is.False);
            Assert.That(stream.PublicationScanCount, Is.EqualTo(scans));
            var p = Target(); map.DestroyTrusted(1, 1, map.World, map.CommitId, new[] { p }, _ => true);
            Assert.That(stream.Publish(), Is.True); Drain(stream, replica);
            Assert.That(replica.Read(p).Cell.IsEmpty, Is.True);
            var late = TerrainMapNetworking.CreateReplica(gameplay, visualDigest);
            var lateStream = TerrainMapNetworking.OpenStream(map, hello, 11, _ => true, () => policy);
            lateStream.Subscribe(new GridBounds(96, -128, 64, 64)); Drain(lateStream, late);
            Assert.That(late.Read(p).Cell.IsEmpty, Is.True);
            permitted = false; policy++;
            Assert.That(stream.Publish(), Is.True); Drain(stream, replica);
            Assert.That(replica.Read(p).TryGetCell(out _), Is.False);
        }
        private static void Drain(MapInterestService stream, ChunkReplicaStateMachine replica)
        {
            byte[] bytes;
            while ((bytes = stream.Dequeue()) != null) replica.Receive(bytes);
            stream.Acknowledge(stream.Commit);
        }
    }
}
