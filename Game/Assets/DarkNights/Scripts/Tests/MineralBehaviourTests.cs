using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AnyRules.Next.Authoring;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using DarkNights.Runtime.Network;
using MemoryPack;
using GameCore.Objects.Definition;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>真实 YYGC 多格状态的事务、最后一份资源及恢复检查；反射仅进入测试所需的内部命中入口，不代替独立进程联机验收。</summary>
    public sealed class MineralBehaviourTests
    {
        [UnityTest]
        public IEnumerator RollbackFrozenCopiesStaleHitsAndRestoreKeepOneObject() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(DarkNights.Editor.Terrain.TerrainTestAssets.DefinitionPath);
            var cells = new byte[320 * 192]; var protectedCells = new bool[cells.Length];
            for (int u = 0; u < 320; u++) { cells[191 * 320 + u] = 8; protectedCells[191 * 320 + u] = true; }
            var blueprint = new TerrainDepositBlueprint("two-cells", "mine", 83, 37, "common", 2,
                new[] { new TerrainMineralCell(83, -37, 1), new TerrainMineralCell(84, -37, 1) });
            var initial = new PlayableTerrain("14223344556647778899001122334455", "mineral-state-fixture", cells, protectedCells,
                new bool[cells.Length], Array.Empty<TerrainRoom>(), new[] { blueprint });
            var world = scope.NewWorld(RuleScenario.Catalog(), PlayableTerrainGenerator.Layout(RuleScenario.Layout()), false,
                terrain: value => new SessionTerrain(value.Context, definition.LoadGameplayCatalog(), initial, definition: definition));
            using var authority = new SessionAuthority(world);
            var deposit = world.Index.MineralDeposits.Single(); int id = deposit.Id, count = world.Index.Count;
            var before = world.CaptureView().MineralDeposits.Single();
            var saved = world.CaptureWorld().MineralDeposits.Single();
            var tool = new MiningToolRules(MiningTargetKinds.MineralDeposit, true, Array.Empty<string>(), Array.Empty<string>(),
                1, deposit.MaximumDurability, 64, 36, .48f, .6f);
            Assert.Throws<InvalidOperationException>(() => world.Mutations.Run<bool>(() =>
            {
                Assert.That(Hit(deposit, tool, 83, -37, 1, out int reward), Is.True); Assert.That(reward, Is.EqualTo(1));
                throw new InvalidOperationException("test rollback");
            }));
            Assert.That(deposit.Remaining, Is.EqualTo(2)); Assert.That(deposit.CaptureState().Cells[0].ContentVersion, Is.EqualTo(1));
            int firstReward = 0;
            Assert.That(world.Mutations.Run(() => Hit(deposit, tool, 83, -37, 1, out firstReward)), Is.True);
            Assert.That(firstReward, Is.EqualTo(1)); Assert.That(deposit.Remaining, Is.EqualTo(1));
            Assert.That(deposit.Capacity, Is.EqualTo(2)); Assert.That(deposit.CaptureState().Cells[0].ContentVersion, Is.EqualTo(2));
            Assert.That(before.Cells[0].Remaining, Is.EqualTo(1)); Assert.That(saved.Cells[0].Durability, Is.EqualTo(deposit.MaximumDurability));
            Assert.That(world.Mutations.Run(() => Hit(deposit, tool, 83, -37, 1, out _)), Is.False);
            int winner = 0, loser = 0;
            Assert.That(world.Mutations.Run(() => Hit(deposit, tool, 84, -37, 1, out winner)), Is.True);
            Assert.That(world.Mutations.Run(() => Hit(deposit, tool, 84, -37, 1, out loser)), Is.False);
            Assert.That(winner + loser, Is.EqualTo(1)); Assert.That(deposit.Stage, Is.EqualTo(MineralDepositStage.Depleted));
            Assert.That(world.Index.Count, Is.EqualTo(count)); Assert.That(world.Index.MineralDeposits.Single().Id, Is.EqualTo(id));
            string json = world.SaveCodec.Serialize(world.CaptureWorld()); world.Restore(json);
            var restored = world.Index.MineralDeposits.Single();
            Assert.That(restored.Id, Is.EqualTo(id)); Assert.That(restored.Remaining, Is.Zero);
            Assert.That(restored.CaptureState().Cells.Length, Is.EqualTo(2));
            Assert.That(restored.CaptureState().Cells.All(cell => cell.ContentVersion == 2 && cell.Durability == 0), Is.True);
            var invalid = JObject.Parse(json); invalid["world"]["mineral_deposits"][0]["cells"][0]["u"] = 82;
            Assert.Throws<FormatException>(() => world.Restore(invalid.ToString()));
            Assert.That(world.Index.MineralDeposits.Single(), Is.SameAs(restored), "坏候选不能退休活动矿床");
        });

        private static bool Hit(MineralDepositBehaviour deposit, MiningToolRules tool, int u, int v, ulong version, out int reward)
        {
            var method = typeof(MineralDepositBehaviour).GetMethod("HitByTool", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(MiningToolRules), typeof(int), typeof(int), typeof(ulong), typeof(int).MakeByRefType() }, null);
            object[] arguments = { tool, u, v, version, 0 };
            bool accepted = (bool)method.Invoke(deposit, arguments); reward = (int)arguments[4]; return accepted;
        }

        [UnityTest]
        public IEnumerator MaximumMineralFootprintsFitReliableProjectionAndFreezeTheirCells() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var catalog = RuleScenario.Catalog(); var layout = RuleScenario.Layout();
            using var authority = SessionScenario.Create(catalog, layout);
            var source = SessionWire.From(authority.CaptureProjection());
            string guid = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("scenery.mineral-deposit").Guid.ToString();
            source.World.MineralWorldId = "14223344556647778899001122334455"; source.World.MineralMapEpoch = 1;
            source.World.MineralDeposits = Enumerable.Range(0, 64).Select(index => new MineralDepositWire
            {
                Id = 1000 + index, X = 1600, Y = 80, RoomKind = "mine", Rarity = "common", ResourceId = "iron",
                MaximumDurability = 40, UnitsPerHarvest = 1, RequiredMiningLevel = 1,
                Cells = Enumerable.Range(index * 64, 64).Select(cell => new MineralCellWire
                { U = cell % 320, V = -80 - cell / 320, Capacity = 1, Remaining = 1, Durability = 40,
                    ContentVersion = 1, ForegroundContentVersion = 17 }).ToArray()
            }).ToArray();
            source.World.Identities = source.World.Identities.Concat(source.World.MineralDeposits.Select(value =>
                new EntityIdentityWire { Id = value.Id, DefinitionGuid = guid, PlacementKey = "terrain.deposit.budget-" + value.Id })).ToArray();
            var frozen = source.Freeze();
            var codec = new ProjectionCodec(catalog, PlayableTerrainGenerator.Layout(layout)); byte[] bytes = codec.Encode(frozen);
            var decoded = codec.Decode(bytes);
            Assert.That(decoded.World.MineralDeposits.Sum(value => value.Cells.Count), Is.EqualTo(4096));
            Assert.That(bytes.Length, Is.LessThanOrEqualTo(ProjectionCodec.MaximumBytes));
            source.World.MineralDeposits[0].Cells[0] = default;
            Assert.That(frozen.World.MineralDeposits[0].Cells[0].Remaining, Is.EqualTo(1));
            Assert.That(decoded.World.MineralDeposits[0].Cells[0].ForegroundContentVersion, Is.EqualTo(17));
            System.IO.Directory.CreateDirectory("../artifacts/embedded-ore-development-20261004/p1-p4");
            System.IO.File.WriteAllText("../artifacts/embedded-ore-development-20261004/p1-p4/mineral-payload.json",
                new JObject { ["context"] = "Editor codec budget probe; transport and real-world density remain separate",
                    ["cells"] = 4096, ["deposits"] = 64, ["encodedBytes"] = bytes.Length,
                    ["rawBytes"] = MemoryPackSerializer.Serialize(SessionWire.From(frozen)).Length }.ToString());
        });
    }
}
