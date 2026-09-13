using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
using DarkNights.Runtime.Objects;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 完整三夜及无人照料的 YYGC 集成回归，重放冻结的正常资源策略。
    /// 一倍速严格比较 Godot 历史报告，二倍速验证单入口与完整结果，实际测量另存证据。
    /// </summary>
    [Category("UnifiedGameplay")]
    public sealed class UnifiedCampaignTests
    {
        [UnityTest]
        public IEnumerator NormalCampaignMatchesFrozenGodotResults() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create(full: true);
            RunStrategy(f, 1);
            JObject expected = RuleScenario.Fixture("godot-gameplay-validation.json");
            ObjectSession world = f.World;
            CampSimulationState camp = world.Camp.CaptureState();
            AssertVictory(world);
            Assert.That(camp.Elapsed, Is.EqualTo((double)expected["playthrough"]["simulation_seconds"]).Within(0.0001));
            Assert.That(world.Economy.Population, Is.EqualTo((int)expected["playthrough"]["survivors"]));
            Assert.That(camp.Lost, Is.EqualTo((int)expected["playthrough"]["losses"]));
            Assert.That(world.Index.Buildings.Single(b => b.RuleKey == "tavern").Hp,
                Is.EqualTo((double)expected["playthrough"]["tavern_hp"]));
            foreach (string resource in GameText.ResourceIds)
                Assert.That(world.Economy.Stock.Get(resource),
                    Is.EqualTo((double)expected["playthrough"]["resources"][resource]).Within(0.0001), resource);
            Report(world, "campaign-1x");
        });

        [UnityTest]
        public IEnumerator DoubleSpeedCampaignCompletesWithStableSaveAndNoFurtherSimulation() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create(full: true);
            RunStrategy(f, 2);
            AssertVictory(f.World);
            string saved = f.World.SaveCodec.Serialize(f.World.CaptureWorld());
            f.Step(20);
            Assert.That(f.World.SaveCodec.Serialize(f.World.CaptureWorld()), Is.EqualTo(saved));
            f.World.Restore(saved);
            Assert.That(f.World.SaveCodec.Serialize(f.World.CaptureWorld()), Is.EqualTo(saved));
            Report(f.World, "campaign-2x");
        });

        [UnityTest]
        public IEnumerator UnattendedCampLosesAtFrozenTimeAndAlsoStopsAtDoubleSpeed() => UniTask.ToCoroutine(async () =>
        {
            for (int speed = 1; speed <= 2; speed++)
            {
                using var f = await UnifiedSliceFixture.Create(full: true);
                f.World.SetTime(false, speed);
                f.Step(1000.0 / speed);
                CampSimulationState camp = f.World.Camp.CaptureState();
                Assert.That(camp.Mode, Is.EqualTo(SessionMode.Lost));
                if (speed == 1)
                {
                    JToken expected = RuleScenario.Fixture("godot-gameplay-validation.json")["idle_playthrough"];
                    Assert.That(camp.Elapsed, Is.EqualTo((double)expected["simulation_seconds"]).Within(0.0001));
                    Assert.That(camp.Kills, Is.EqualTo((int)expected["kills"]));
                }
                Report(f.World, "idle-" + speed + "x");
            }
        });

        private static void RunStrategy(UnifiedSliceFixture f, int speed)
        {
            ObjectSession world = f.World;
            world.SetTime(false, speed);
            ActorBehaviour[] workers = world.Index.Actors.Where(a => !a.Enemy && a.RuleKey == "worker").ToArray();
            int[] production = { workers[0].Id, workers[1].Id, workers[4].Id };
            for (int i = 0; i < 2; i++)
                Assert.That(world.PlaceBuilding("tower", 708 + i * 56, new[] { workers[i].Id }), Is.GreaterThan(0));
            Assert.That(world.TrainActors("archer", new[] { workers[2].Id, workers[3].Id }), Is.EqualTo(2));
            WorksiteBehaviour food = world.Index.Worksites.First(s => s.RuleKey == "food");
            world.IssueOrders(new[] { workers[4].Id }, food.Id, food.X);
            for (int second = 0; second < 720 && world.Camp.CaptureState().Mode == SessionMode.Playing; second++)
            {
                if (second >= 40 && world.Economy.Population < world.Economy.Capacity &&
                    world.Economy.CaptureState().RecruitCooldown <= 0 && world.Economy.CanPay(new ResourceAmounts(food: 25)))
                    world.Recruit();
                foreach (ActorBehaviour actor in world.Index.Actors.ToArray())
                {
                    if (actor.Enemy) continue;
                    if (actor.RuleKey == "worker" && actor.Activity == ActorActivity.Idle)
                    {
                        if (production.Contains(actor.Id))
                        {
                            WorksiteBehaviour site = world.Index.Worksites.FirstOrDefault(s => s.RuleKey == "wood" && s.WorkerId == 0 && s.Amount != 0);
                            if (site != null) world.IssueOrders(new[] { actor.Id }, site.Id, site.X);
                        }
                        else world.TrainActors("spearman", new[] { actor.Id });
                    }
                    else if (actor.RuleKey != "worker" && actor.Activity == ActorActivity.Idle)
                    {
                        float rally = actor.RuleKey == "archer" ? 712 : 766;
                        if (Math.Abs(actor.X - rally) > 2) world.IssueOrders(new[] { actor.Id }, 0, rally);
                    }
                }
                foreach (BuildingBehaviour building in world.Index.Buildings)
                    if (building.IsComplete && building.Hp < building.MaximumHp - 80 &&
                        world.Economy.CanPay(world.Catalog.Balance.Economy.RepairCost)) world.Repair(building.Id);
                f.Step(1.0 / speed);
            }
        }

        private static void AssertVictory(ObjectSession world)
        {
            Assert.That(world.Camp.CaptureState().Mode, Is.EqualTo(SessionMode.Won));
            Assert.That(world.Camp.CaptureState().Kills, Is.EqualTo(34));
            Assert.That(world.Waves.CaptureState().Index, Is.EqualTo(2));
            Assert.That(world.Index.EnemyCount, Is.Zero);
        }

        private static void Report(ObjectSession world, string name)
        {
            const string folder = "../artifacts/yygc-unified/rule-runs";
            Directory.CreateDirectory(folder);
            File.WriteAllText(folder + "/" + name + ".json", new JObject
            {
                ["mode"] = world.Camp.CaptureState().Mode.ToString(), ["elapsed"] = world.Elapsed,
                ["kills"] = world.Camp.CaptureState().Kills, ["lost"] = world.Camp.CaptureState().Lost,
                ["population"] = world.Economy.Population, ["resources"] = JObject.FromObject(world.Economy.Stock),
                ["world"] = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()))
            }.ToString());
        }
    }
}
