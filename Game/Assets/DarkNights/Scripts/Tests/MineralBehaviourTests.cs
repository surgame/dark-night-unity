using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Networking;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>正式原生矿层的事务、容量与恢复验证；替代旧逐矿床实例路线，独立 Player 联机另行验收。</summary>
    public sealed class MineralBehaviourTests
    {
        private static PlayableTerrain Data(TerrainDepositBlueprint[] deposits)
        {
            var cells = new byte[320 * 192];
            for (int u = 0; u < 320; u++) cells[191 * 320 + u] = 8;
            return new PlayableTerrain("14223344556647778899001122334455", "native-mineral-fixture", cells, new bool[cells.Length],
                new bool[cells.Length], Array.Empty<TerrainRoom>(), deposits);
        }
        private static ObjectSession World(UnifiedSessionScope scope, PlayableTerrain data)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(DarkNights.Editor.Terrain.TerrainTestAssets.DefinitionPath);
            return scope.NewWorld(RuleScenario.Catalog(), PlayableTerrainGenerator.Layout(RuleScenario.Layout()), false,
                terrain: value => new SessionTerrain(value.Context, definition.LoadGameplayCatalog(), data, definition: definition));
        }
        private static bool Hit(MineralMapAuthority map, CellCoord cell, int damage, out int reward)
        {
            var method = typeof(MineralMapAuthority).GetMethod("StageHit", BindingFlags.Instance | BindingFlags.NonPublic);
            object[] arguments = { cell, damage, 0 }; bool result = (bool)method.Invoke(map, arguments);
            reward = (int)arguments[2]; return result;
        }
        [UnityTest]
        public IEnumerator NativeCellsRollbackResetDepleteAndRestoreWithoutObjects() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var initial = Data(new[] { new TerrainDepositBlueprint("two", "mine", 83, 37, "common", 3,
                new[] { new TerrainMineralCell(83, -37, 2), new TerrainMineralCell(84, -37, 1) }) });
            var world = World(scope, initial); using var authority = new SessionAuthority(world);
            var map = world.Terrain.Minerals; var target = new CellCoord(83, -37); int entities = world.Index.Count;
            var frozen = map.Capture(); ulong commit = map.CommitId;
            Assert.That(world.Index.MineralDeposits, Is.Empty); Assert.That(world.CaptureView().MineralDeposits, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => world.Mutations.Run<bool>(() =>
            { Hit(map, target, 40, out _); throw new InvalidOperationException("abort mineral batch"); }));
            Assert.That(map.Query(target).State.RemainingReserves, Is.EqualTo(2)); Assert.That(map.CommitId, Is.EqualTo(commit));
            Assert.That(world.Mutations.Run(() => Hit(map, target, 10, out _)), Is.True);
            Assert.That(map.Query(target).State.Durability, Is.EqualTo(30)); Assert.That(frozen.Durability(37 * 320 + 83), Is.EqualTo(40));
            int reward = 0;
            Assert.That(world.Mutations.Run(() => Hit(map, target, 30, out reward)), Is.True); Assert.That(reward, Is.EqualTo(1));
            Assert.That(map.Query(target).State.Durability, Is.EqualTo(40)); Assert.That(map.Query(target).State.RemainingReserves, Is.EqualTo(1));
            string partial = world.SaveCodec.Serialize(world.CaptureWorld());
            Assert.That(world.Mutations.Run(() => Hit(map, target, 40, out reward)), Is.True); Assert.That(reward, Is.EqualTo(1));
            Assert.That(map.Read(target).Cell.IsEmpty, Is.True); Assert.That(map.Query(target).HasState, Is.False);
            Assert.That(world.Mutations.Run(() => Hit(map, target, 40, out reward)), Is.False); Assert.That(reward, Is.Zero);
            string depleted = world.SaveCodec.Serialize(world.CaptureWorld()); world.Restore(depleted);
            Assert.That(world.Terrain.Minerals.Read(target).Cell.IsEmpty, Is.True); Assert.That(world.Index.Count, Is.EqualTo(entities));
            world.Restore(partial); map = world.Terrain.Minerals;
            Assert.That(map.Query(target).State.RemainingReserves, Is.EqualTo(1)); Assert.That(map.Query(target).State.Durability, Is.EqualTo(40));
            var invalid = JObject.Parse(partial); invalid["world"]["terrain"]["mineral_map"] = "broken";
            Assert.Throws<FormatException>(() => world.Restore(invalid.ToString())); Assert.That(world.Terrain.Minerals, Is.SameAs(map));
        });
        [UnityTest]
        public IEnumerator WallRemovalRevealsOnlyPartOfDepositAndPreservesMineralState() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var initial = Data(new[] { new TerrainDepositBlueprint("covered", "mine", 83, 37, "common", 3,
                new[] { new TerrainMineralCell(83, -37, 2), new TerrainMineralCell(84, -37, 1) }) });
            var cells = initial.CopyMaterials(); cells[37 * 320 + 83] = cells[37 * 320 + 84] = 2;
            var data = new PlayableTerrain(initial.WorldId, initial.Seed, cells, new bool[cells.Length], new bool[cells.Length],
                Array.Empty<TerrainRoom>(), initial.Deposits.ToArray());
            var world = World(scope, data); using var authority = new SessionAuthority(world);
            var wall = world.Terrain.Map; var ore = world.Terrain.Minerals; var target = new CellCoord(83, -37);
            float x = TerrainMiningGeometry.CenterX(80), height = TerrainMiningGeometry.CenterHeight(-37);
            Assert.That(MineralMiningQuery.First(ore, wall, x, height, 1, 0, 64, out _, out _), Is.False);
            int count = world.Index.Count; ulong oreCommit = ore.CommitId;
            for (int hit = 0; hit < 32 && !wall.Read(target).Cell.IsEmpty; hit++)
                wall.DestroyTrusted(1, "reveal:" + hit, TerrainEditAction.HandMine, wall.World, target, new[] { target }, _ => true);
            Assert.That(wall.Read(target).Cell.IsEmpty, Is.True);
            Assert.That(wall.Read(new CellCoord(84, -37)).Cell.IsEmpty, Is.False, "仅拆目标墙格，矿床另一部分仍覆盖。");
            Assert.That(MineralMiningQuery.First(ore, wall, x, height, 1, 0, 64, out var found, out _), Is.True);
            Assert.That(found, Is.EqualTo(target)); Assert.That(ore.CommitId, Is.EqualTo(oreCommit));
            Assert.That(ore.Query(target).State.Durability, Is.EqualTo(40)); Assert.That(ore.Query(target).State.RemainingReserves, Is.EqualTo(2));
            Assert.That(ore.Query(new CellCoord(84, -37)).State.RemainingReserves, Is.EqualTo(1));
            Assert.That(world.Index.Count, Is.EqualTo(count)); Assert.That(world.Index.MineralDeposits, Is.Empty);
        });
        [UnityTest]
        public IEnumerator LocalRegionWaitsForCurrentBaselineAndLoadsCompleteBoundaryChunks() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = World(scope, Data(Array.Empty<TerrainDepositBlueprint>()));
            using var authority = new SessionAuthority(world);
            var map = world.Terrain.Minerals; var rules = map.Rules;
            var replica = TerrainMapNetworking.CreateReplica(rules.Business.Gameplay, rules.Definition.AuthoringSourceDigest, rules.Business);
            var handshake = new MapHandshake(map.Descriptor, rules.Business.ContentDigest, rules.Definition.AuthoringSourceDigest,
                rules.Business.Gameplay.Definitions.Select(value => value.Identity.Guid).ToArray());
            using var stream = new MapInterestService(map, map.Tiles, handshake, 1, _ => true, () => map.CommitId, () => 1);
            void Flush() { byte[] packet; while ((packet = stream.Dequeue()) != null) replica.ReceivePacket(packet); stream.Acknowledge(replica.CommitId); }
            var first = SessionMineralNetwork.Around(320, 0);
            stream.Subscribe(MineralRegionReadiness.Subscription(first, map.Descriptor.Bounds)); Flush();
            Assert.That(MineralRegionReadiness.Complete(replica, replica.Descriptor, first), Is.True);
            Assert.That(replica.Read(new CellCoord((int)first.MaxUExclusive, first.MinV)).State, Is.EqualTo(GridSampleState.Unknown),
                "协议 halo 恰好补全所需区块，不保留外侧半知区块。");
            var moved = SessionMineralNetwork.Around(1800, -1500);
            Assert.That(replica.CommitId, Is.GreaterThan(0));
            Assert.That(MineralRegionReadiness.Complete(replica, replica.Descriptor, moved), Is.False,
                "旧区域的完整提交不能放行新区域。");
            stream.Subscribe(MineralRegionReadiness.Subscription(moved, map.Descriptor.Bounds)); Flush();
            Assert.That(MineralRegionReadiness.Complete(replica, replica.Descriptor, moved), Is.True);
            Assert.That(MineralRegionReadiness.Complete(replica, replica.Descriptor, first), Is.False);
            var bottom = SessionMineralNetwork.Around(5104, -10000);
            stream.Subscribe(MineralRegionReadiness.Subscription(bottom, map.Descriptor.Bounds)); Flush();
            Assert.That(bottom.MinV, Is.EqualTo(-191)); Assert.That(bottom.MaxVExclusive % 32, Is.Zero);
            Assert.That(MineralRegionReadiness.Complete(replica, replica.Descriptor, bottom), Is.True);
            var input = new DarkNights.View.Terrain.TerrainReplicaSource(replica);
            for (int v = GridMath.FloorDiv(bottom.MinV, 32); v < bottom.MaxVExclusive / 32; v++)
                for (int u = bottom.MinU / 32; u < bottom.MaxUExclusive / 32; u++)
                    await input.LoadAsync(replica.Descriptor, new ChunkCoord(u, v), default);
            input.PublishInitialBaseline();
        });
        [UnityTest]
        public IEnumerator FullMapMineralsFitSaveAndDoNotConsumeEntityOrProjectionBudget() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var deposits = new List<TerrainDepositBlueprint>();
            for (int start = 0; start < 320 * 192; start += 64)
            {
                var cells = Enumerable.Range(start, 64).Select(i => new TerrainMineralCell(i % 320, -i / 320, 1 + i % 997)).ToArray();
                deposits.Add(new TerrainDepositBlueprint("dense-" + start, "mine", start % 320, start / 320,
                    start % 128 == 0 ? "common" : "rare", cells.Sum(c => c.Capacity), cells));
            }
            var timer = Stopwatch.StartNew(); var world = World(scope, Data(deposits.ToArray()));
            using var authority = new SessionAuthority(world); long prepareMs = timer.ElapsedMilliseconds;
            Assert.That(world.Index.MineralDeposits, Is.Empty);
            Assert.That(world.Terrain.Minerals.Capture().Count, Is.EqualTo(61440));
            Assert.That(world.Terrain.Minerals.Query(new CellCoord(319, -191)).State.RemainingReserves, Is.EqualTo(1 + 61439 % 997));
            timer.Restart(); string json = world.SaveCodec.Serialize(world.CaptureWorld()); int bytes = Encoding.UTF8.GetByteCount(json);
            long saveMs = timer.ElapsedMilliseconds;
            Assert.That(bytes, Is.LessThanOrEqualTo(4000000));
            var captured = world.SaveCodec.Parse(json); Assert.That(captured.MineralDeposits, Is.Empty);
            timer.Restart(); world.Restore(json); long restoreMs = timer.ElapsedMilliseconds;
            Assert.That(world.Terrain.Minerals.Query(new CellCoord(0, 0)).State.RemainingReserves, Is.EqualTo(1));
            Assert.That(world.Index.MineralDeposits, Is.Empty);
            System.IO.Directory.CreateDirectory("../artifacts/mineral-map-migration-20261005");
            System.IO.File.WriteAllText("../artifacts/mineral-map-migration-20261005/dense-save.json", new JObject
            { ["cells"] = 61440, ["staticBeds"] = deposits.Count, ["mineralObjects"] = 0, ["saveBytes"] = bytes,
                ["prepareMs"] = prepareMs, ["saveMs"] = saveMs, ["restoreMs"] = restoreMs,
                ["scope"] = "Editor full-map data/save capacity; no foreground FPS claim" }.ToString());
        });
    }
}
