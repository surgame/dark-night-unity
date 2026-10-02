using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>Definition 驱动采集的匹配、真实 YYGC 装配及装备身份恢复验收；临时工具只存在于隔离测试目录的内存注册表。</summary>
    public sealed class MiningToolDefinitionTests
    {
        [SetUp]
        public void PrepareDefinitionIndex() => ObjectDefinitionDatabase.Instance.RebuildLookup();

        [Test]
        public void CategoryMaterialLevelAndDefinitionMustAllMatch()
        {
            string deposit = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("scenery.mineral-deposit").Guid.ToString();
            var rule = new MiningToolRules(MiningTargetKinds.MineralDeposit, false, new[] { "iron" },
                new[] { deposit }, 2, 26, 48, 36, .48f, .6f);
            Assert.That(rule.BlockReason(HeroMiningTargetKind.MineralDeposit, "iron", 2, deposit), Is.Empty);
            Assert.That(rule.BlockReason(HeroMiningTargetKind.Foreground, "iron", 1, deposit), Is.Not.Empty);
            Assert.That(rule.BlockReason(HeroMiningTargetKind.MineralDeposit, "gold", 1, deposit), Is.Not.Empty);
            Assert.That(rule.BlockReason(HeroMiningTargetKind.MineralDeposit, "iron", 3, deposit), Is.Not.Empty);
            Assert.That(rule.BlockReason(HeroMiningTargetKind.MineralDeposit, "iron", 1, new string('1', 32)), Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator SecondDefinitionAssemblesOwnFrozenAbilityAndRestoresBothIdentities() => UniTask.ToCoroutine(async () =>
        {
            var database = ObjectDefinitionDatabase.Instance;
            bool dirty = UnityEditor.EditorUtility.IsDirty(database);
            var original = database.GetDefinitionByKey("item.pickaxe");
            var second = ScriptableObject.CreateInstance<ObjectDefinition>();
            second.Name = "测试采集工具"; second.Type = original.Type; second.NetType = original.NetType; second.PrefabRef = original.PrefabRef;
            second.BehaviourTypes = new System.Collections.Generic.List<string>(original.BehaviourTypes);
            second.SharedConfigs.Add(new EquipmentItemConfig { RuleKey = "pickaxe", Handheld = HeroEquipmentKind.Pickaxe });
            var custom = new MiningToolConfig { Damage = 26, Targets = MiningTargetKinds.MineralDeposit,
                AllMaterials = false, Materials = new[] { "iron" }, Level = 2 };
            second.SharedConfigs.Add(custom);
            second.EditorSetIdentity(Guid.NewGuid().ToString("N"), "item.test-second-mining-tool", false);
            database.AddDefinition(second);
            try
            {
                using var scope = await UnifiedSessionScope.Create();
                var world = scope.NewWorld(RuleScenario.Catalog(), RuleScenario.Layout());
                var actor = world.Index.Actors[0];
                int actorId = actor.Id;
                var save = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
                var data = save["world"]["actors"].OfType<JObject>().Single(value => (int)value["id"] == actor.Id);
                data["slot_0"] = original.Guid.ToString(); data["slot_1"] = second.Guid.ToString();
                data["selected_item"] = 1; data["inventory_revision"] = 1;
                world.Restore(save.ToString());
                actor = world.Index.Find<ActorBehaviour>(actorId);
                var inventory = actor.Object.GetBehaviour<HeroInventoryBehaviour>();
                var tool = (MiningToolBehaviour)typeof(HeroInventoryBehaviour).GetMethod("MiningTool", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(inventory, null);
                Assert.That(tool, Is.Not.Null);
                Assert.That(tool.Rules.Damage, Is.EqualTo(26));
                Assert.That(tool.Rules.BlockReason(HeroMiningTargetKind.MineralDeposit, "iron"), Is.Empty);
                Assert.That(tool.Rules.BlockReason(HeroMiningTargetKind.MineralDeposit, "gold"), Is.Not.Empty);
                Assert.That(tool.Rules.BlockReason(HeroMiningTargetKind.Foreground, "iron"), Is.Not.Empty);
                custom.Damage = 70;
                Assert.That(tool.Rules.Damage, Is.EqualTo(26), "运行工具只消费启动时冻结目录。");
                custom.Damage = 26;
                var projected = world.CaptureView().Actors.Single(value => value.Id == actor.Id);
                Assert.That(projected.Slot0, Is.EqualTo(projected.Slot1), "视觉类型可相同，装备身份仍不同。");
                Assert.That(projected.Slot0Definition, Is.EqualTo(original.Guid.ToString()));
                Assert.That(projected.Slot1Definition, Is.EqualTo(second.Guid.ToString()));
                world.Restore(world.SaveCodec.Serialize(world.CaptureWorld()));
                var restored = world.Index.Find<ActorBehaviour>(actorId).CaptureState();
                Assert.That(restored.Slot0, Is.EqualTo(original.Guid.ToString()));
                Assert.That(restored.Slot1, Is.EqualTo(second.Guid.ToString()));
            }
            finally
            {
                database.Definitions.Remove(second); database.RebuildLookup();
                UnityEngine.Object.DestroyImmediate(second);
                if (!dirty) UnityEditor.EditorUtility.ClearDirty(database);
            }
        });

        [UnityTest]
        public IEnumerator SwitchingToolCancelsPendingHitAndOldEquipmentIdentity() => UniTask.ToCoroutine(async () =>
        {
            using var fixture = await HeroTestSession.Create();
            fixture.SeedLoadout(0, 2);
            fixture.Command(Runtime.Session.SessionOperation.ClaimHero);
            fixture.Authority.SubmitInput(fixture.Host, fixture.Packet(useHeld: true, usePressed: true));
            fixture.Authority.Tick();
            Assert.That(fixture.State.PickaxeHitPending, Is.True);
            fixture.Command(Runtime.Session.SessionOperation.SelectHeroItem, value: 1);
            fixture.Authority.Tick();
            Assert.That(fixture.State.PickaxeHitPending, Is.False);
            Assert.That(fixture.State.MiningToolDefinition, Is.Empty);
        });
    }
}
