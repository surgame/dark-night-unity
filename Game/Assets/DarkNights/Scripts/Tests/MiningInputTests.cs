using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Definition;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>隔离 YYGC 会话中的显式采矿输入回归；夹具安排可达目标，不代表正式地图路线或实际鼠标验收。</summary>
    public sealed class MiningInputTests
    {
        [UnityTest]
        public IEnumerator SoftRockClickDamagesOnlyTargetAndRejectsDuplicate() => Run(2, false, true, true, "");

        [UnityTest]
        public IEnumerator OreClickDamagesBeforeReward() => Run(5, false, false, true, "");

        [UnityTest]
        public IEnumerator HardRockCanBeDamaged() => Run(2, false, false, true, "");

        [UnityTest]
        public IEnumerator ProtectedOreCanBeDamaged() => Run(5, true, false, true, "");

        [UnityTest]
        public IEnumerator BedrockCannotBeMined() => Run(8, false, false, false, "基岩不能破坏");

        [UnityTest]
        public IEnumerator EmptyCellHasNoEffectAndExplainsWhy() => Run(0, false, false, false, "这里没有可采集的矿床");

        [UnityTest]
        public IEnumerator MineralDepositClickDamagesBeforeHarvest() => Run(0, false, false, true, "这里没有可采集的矿床", true);

        [UnityTest]
        public IEnumerator DisabledDepositForgedTargetCannotDamageOrReward() => Run(0, false, false, false,
            "这里没有可采集的矿床", true, false);

        private static IEnumerator Run(byte material, bool protect, bool soft, bool succeeds, string reason,
            bool deposit = false, bool allowDeposit = true) =>
            UniTask.ToCoroutine(async () =>
            {
                var config = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/ShipTrade/item-pickaxe.asset")
                    .SharedConfigs.OfType<MiningToolConfig>().Single();
                var previous = config.Targets;
                try
                {
                    config.Targets = DarkNights.Core.Config.MiningTargetKinds.Foreground | (allowDeposit ? DarkNights.Core.Config.MiningTargetKinds.MineralDeposit : 0);
                    await Execute(material, protect, soft, succeeds, reason, deposit, allowDeposit);
                }
                finally { config.Targets = previous; }
            });

        private static async UniTask Execute(byte material, bool protect, bool soft, bool succeeds, string reason,
            bool deposit, bool allowDeposit)
        {
            using var scope = await UnifiedSessionScope.Create();
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(Editor.Terrain.TerrainTestAssets.DefinitionPath);
            var catalog = RuleScenario.Catalog();
            var layout = PlayableTerrainGenerator.Layout(RuleScenario.Layout());
            var cells = new byte[TerrainGenerationSettings.Width * TerrainGenerationSettings.Height];
            var protection = new bool[cells.Length];
            var softRock = new bool[cells.Length];
            for (int row = PlayableTerrain.CampRow; row < TerrainGenerationSettings.Height; row++)
                for (int column = 0; column < TerrainGenerationSettings.Width; column++)
                {
                    int floor = row * TerrainGenerationSettings.Width + column;
                    bool bottom = row == TerrainGenerationSettings.Height - 1;
                    cells[floor] = bottom ? (byte)8 : (byte)2;
                    protection[floor] = bottom || column < PlayableTerrain.CampColumns && row < PlayableTerrain.CampRow + 4;
                }
            const float actorX = 1300;
            int targetU = TerrainMiningGeometry.CellU(actorX) + 1;
            int targetRow = PlayableTerrain.CampRow - 3;
            int index = targetRow * TerrainGenerationSettings.Width + targetU;
            cells[index] = material; protection[index] = protect; softRock[index] = soft;
            var deposits = deposit ? new[] { new TerrainDepositBlueprint("click-deposit", "mine", targetU, targetRow, "common", 60) }
                : Array.Empty<TerrainDepositBlueprint>();
            var terrain = new PlayableTerrain("1ab9876234564cde8abc012345678901", "mining-input-fixture",
                cells, protection, softRock, Array.Empty<TerrainRoom>(), deposits);
            var world = scope.NewWorld(catalog, layout, false, terrain: value =>
                new SessionTerrain(value.Context, definition.LoadGameplayCatalog(), terrain));
            using var authority = new SessionAuthority(world);
            var host = authority.Connect(0);
            authority.AcknowledgeReady(host, authority.Epoch, authority.Revision);
            int actorId = world.Index.Actors[0].Id;
            var save = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
            var saved = save["world"]["actors"].OfType<JObject>().Single(value => (int)value["id"] == actorId);
            saved["slot_0"] = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("item.pickaxe").Guid.ToString(); saved["inventory_revision"] = 1;
            saved["x"] = actorX;
            saved["height"] = 0;
            world.Restore(save.ToString());
            var request = new SessionRequest(SessionOperation.ClaimHero, SessionAuthority.ProtocolVersion,
                authority.Epoch, authority.PolicyRevision, 1, new[] { actorId });
            authority.Submit(host, request); authority.Tick();
            var actor = world.Index.Find<ActorBehaviour>(actorId);
            var state = actor.CaptureState();
            var map = world.Terrain.Map;
            var target = new CellCoord(targetU, -targetRow);
            var before = map.Read(target).Cell;
            var mineral = deposit ? world.Index.MineralDeposits.OfType<MineralDepositBehaviour>().Single() : null;
            int durability = deposit ? mineral.Durability : before.IsEmpty ? 0 : map.Query(target).State.Durability;
            var handheld = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("item.pickaxe")
                .SharedConfigs.OfType<MiningToolConfig>().Single();
            int baseDamage = handheld.Damage;
            int damage = deposit ? baseDamage : before.IsEmpty ? 0 : map.Rules.PickaxeDamage(before.TileId, baseDamage);
            Assert.That(TerrainMiningQuery.CanMine(map, map.Tiles, target), Is.EqualTo(succeeds && !deposit));
            Assert.That(TerrainMiningQuery.BlockReason(map, map.Tiles, target), Does.StartWith(reason));
            float handHeight = handheld.HandHeight;
            Assert.That(TerrainMiningQuery.Reachable(map, state.X, state.Height + handHeight,
                target, handheld.Reach), Is.True);
            if (deposit)
            {
                Assert.That(mineral.RequiredMiningLevel, Is.EqualTo(1));
                Assert.That(world.CaptureView().Worksites.Single(value => value.Id == mineral.Id).RequiredMiningLevel, Is.EqualTo(1));
            }
            var mining = new HeroMiningTarget(map.World.WorldId.ToString().Replace("-", ""), map.World.Epoch,
                target.U, target.V, before.TileId, before.Flags,
                deposit ? HeroMiningTargetKind.MineralDeposit : HeroMiningTargetKind.Foreground,
                mineral?.Id ?? 0, map.ContentVersion(target));
            var input = new HeroInputRequest(SessionAuthority.ProtocolVersion, authority.Epoch, authority.PolicyRevision,
                actorId, state.ControlLease, 1, authority.ServerTick, 0, false, false, false, false,
                0, state.SelectionRevision, true, false, false, false, mining);
            double iron = world.Economy.CaptureState().Iron;
            Assert.That(authority.SubmitInput(host, input), Is.True);
            authority.Tick();
            Assert.That(actor.CaptureState().EquipmentAction, Is.GreaterThan(0));
            Assert.That(world.Economy.CaptureState().Iron, Is.EqualTo(iron), "落镐前不能结算资源。");
            if (deposit) Assert.That(mineral.Durability, Is.EqualTo(durability));
            for (int tick = 0; tick < Math.Ceiling(handheld.Seconds * handheld.ImpactFraction * 60) + 1; tick++)
                authority.Tick();
            Assert.That(map.Read(target).Cell.IsEmpty, Is.EqualTo(material == 0 || succeeds && !deposit && damage >= durability));
            int reward = succeeds && damage >= durability && (material == 5 || deposit) ? 1 : 0;
            Assert.That(world.Economy.CaptureState().Iron - iron, Is.EqualTo(reward));
            if (deposit)
            {
                Assert.That(mineral.Remaining, Is.EqualTo(60 - reward));
                int expectedDurability = durability;
                if (succeeds) expectedDurability = damage >= durability ? mineral.MaximumDurability : durability - damage;
                Assert.That(mineral.Durability, Is.EqualTo(expectedDurability));
            }
            else if (succeeds && damage < durability) Assert.That(map.Query(target).State.Durability, Is.EqualTo(durability - damage));
            Assert.That(authority.SubmitInput(host, input), Is.False);
            authority.Tick();
            Assert.That(world.Economy.CaptureState().Iron - iron, Is.EqualTo(reward));
            Assert.That(map.Read(new CellCoord(targetU + 1, -PlayableTerrain.CampRow)).Cell.IsEmpty, Is.False);
            if (deposit)
            {
                if (!allowDeposit)
                {
                    int remaining = mineral.Remaining;
                    Assert.That(world.Mutations.Run(() => (bool)typeof(MineralDepositBehaviour)
                        .GetMethod("Extract", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(mineral, new object[] { 1 })), Is.True);
                    Assert.That(mineral.Remaining, Is.EqualTo(remaining - 1), "独立采集能力不受矿镐开关影响。");
                }
                int savedRemaining = mineral.Remaining;
                world.Restore(world.SaveCodec.Serialize(world.CaptureWorld()));
                var restored = world.Index.MineralDeposits.Single();
                Assert.That(restored.Remaining, Is.EqualTo(savedRemaining));
                Assert.That(restored.RequiredMiningLevel, Is.EqualTo(1));
            }
        }
    }
}
