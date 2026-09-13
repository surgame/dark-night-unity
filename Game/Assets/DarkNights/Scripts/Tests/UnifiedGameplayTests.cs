using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Logic.State;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 完整 YYGC 玩法的原规则、跨对象原子性和新档恢复回归，使用真实 Prefab 与装配。
    /// 数值及时间来自冻结规则场景，测试不会重生成旧证据或构造第二套运行世界。
    /// </summary>
    [Category("UnifiedGameplay")]
    public sealed class UnifiedGameplayTests
    {
        [UnityTest]
        public IEnumerator InitialOrderAndFarmCompletionPreserveRelationships() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create(full: true);
            ObjectSession world = f.World;
            Assert.That(world.Index.Count, Is.EqualTo(17));
            Assert.That(world.Economy.Population, Is.EqualTo(7));
            Assert.That(world.Economy.Capacity, Is.EqualTo(9));
            Assert.That(world.Index.Find<BuildingBehaviour>(4).FarmSiteId, Is.EqualTo(5));
            Assert.That(world.Index.Find<WorksiteBehaviour>(5).FarmId, Is.EqualTo(4));
            Assert.That(world.Index.Actors.Select(a => a.Id), Is.EqualTo(Enumerable.Range(11, 7)));
            Assert.That(world.Camp.CaptureState().NextEntityId, Is.EqualTo(18));
            int farm = world.PlaceBuilding("farm", 184, new[] { 11 });
            Assert.That(farm, Is.EqualTo(18));
            f.Step(9);
            Assert.That(world.Index.Find<BuildingBehaviour>(farm).FarmSiteId, Is.EqualTo(19));
            WorksiteBehaviour site = world.Index.Find<WorksiteBehaviour>(19);
            Assert.That(site.FarmId, Is.EqualTo(farm));
            Assert.That(site.WorkerId, Is.EqualTo(11));
            Assert.That(world.Index.Find<ActorBehaviour>(11).TargetId, Is.EqualTo(site.Id));
            Assert.That(world.Economy.Stock.Wood, Is.EqualTo(80));
            Assert.That(world.SaveCodec.Parse(Save(f)).Worksites.Count, Is.EqualTo(7));
        });

        [UnityTest]
        public IEnumerator TrainingReplacesProfessionOnceAndPreservesSceneIdentity() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create(true, true);
            ObjectSession world = f.World;
            var original = f.SceneWorker;
            string placement = world.Index.Find<ActorBehaviour>(11).PlacementKey;
            world.IssueOrders(new[] { 11 }, 6, 402);
            Assert.That(world.TrainActors("spearman", new[] { 11, 13 }), Is.EqualTo(2));
            BuildingState frozen = world.Index.Find<BuildingBehaviour>(3).CaptureState();
            Assert.That(world.Index.Find<WorksiteBehaviour>(6).WorkerId, Is.Zero);
            Assert.That(world.IssueOrders(new[] { 11 }, 6, 402), Is.Zero);
            frozen.TrainingQueue[0] = new TrainingStateEntry(999, "archer", 0);
            Assert.That(world.Index.Find<BuildingBehaviour>(3).CaptureState().TrainingQueue[0].ActorId, Is.EqualTo(11));
            f.Step(20);
            ActorBehaviour trained = world.Index.Find<ActorBehaviour>(11);
            Assert.That(trained.RuleKey, Is.EqualTo("spearman"));
            Assert.That(trained.Object, Is.Not.SameAs(original));
            Assert.That(original.IsActive, Is.False);
            Assert.That(trained.PlacementKey, Is.EqualTo(placement));
            Assert.That(trained.Name, Is.EqualTo("艾达"));
            Assert.That(world.Index.Find<ActorBehaviour>(13).RuleKey, Is.EqualTo("worker"));
            f.Step(20);
            Assert.That(world.Index.Find<ActorBehaviour>(13).Hp, Is.EqualTo(30));
            Assert.That(world.Economy.Population, Is.EqualTo(7));
            Assert.That(world.Index.Actors.Select(a => a.Id), Is.EqualTo(Enumerable.Range(11, 7)));
            string saved = Save(f);
            world.Restore(saved);
            Assert.That(Save(f), Is.EqualTo(saved));
            Assert.That(world.Index.Find<ActorBehaviour>(11).Object, Is.Not.SameAs(original));
            using var replica = new ObjectReplica(f.Resources, f.Placements.Select(p =>
                new ObjectPlacement(p.PlacementKey, p.Definition, p.X, p.Variant, p.ActorName, null)).ToArray(), null);
            replica.Apply(world.CaptureView());
            Assert.That(replica.Count, Is.EqualTo(17));
        });

        [UnityTest]
        public IEnumerator DestructionRefundsTrainingAndReleasesFarmWork() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create(full: true);
            ObjectSession world = f.World;
            Assert.That(world.TrainActors("archer", new[] { 11, 12 }), Is.EqualTo(2));
            double food = world.Economy.Stock.Food;
            UnifiedGameplayProbe.Damage(world, world.Index.Find<BuildingBehaviour>(3), 1000);
            Assert.That(world.Index.Find(3), Is.Null);
            Assert.That(world.Index.Find<ActorBehaviour>(11).IsTraining, Is.False);
            Assert.That(world.Economy.Stock.Food, Is.EqualTo(food + 20));
            world.IssueOrders(new[] { 15 }, 4, 330);
            UnifiedGameplayProbe.Damage(world, world.Index.Find<BuildingBehaviour>(4), 1000);
            Assert.That(world.Index.Find(5), Is.Null);
            Assert.That(world.Index.Find<ActorBehaviour>(15).TargetId, Is.Zero);
            UnifiedGameplayProbe.Damage(world, world.Index.Find<BuildingBehaviour>(2), 1000);
            Assert.That(world.Camp.CaptureState().Mode, Is.EqualTo(SessionMode.Lost));
            string ended = Save(f);
            world.Restore(ended);
            Assert.That(Save(f), Is.EqualTo(ended));
        });

        [UnityTest]
        public IEnumerator ArmorFlightAndBountyUseOneDamageOwner() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create(full: true);
            ActorBehaviour armored = UnifiedGameplayProbe.Spawn(f.World, "armored", 970);
            UnifiedGameplayProbe.Damage(f.World, armored, 1);
            Assert.That(armored.Hp, Is.EqualTo(39));
            ActorBehaviour enemy = UnifiedGameplayProbe.Spawn(f.World, "zombie", 950);
            UnifiedGameplayProbe.Shoot(f.World, 870, enemy, 5);
            ProjectileState frozen = f.World.Projectiles.CaptureState();
            Assert.That(enemy.Hp, Is.EqualTo(20));
            f.Step(1);
            Assert.That(enemy.Hp, Is.EqualTo(15));
            Assert.That(f.World.Projectiles.CaptureState().Shots, Is.Empty);
            Assert.That(frozen.Shots[0].Age, Is.Zero);
            int id = enemy.Id;
            UnifiedGameplayProbe.Damage(f.World, enemy, 100);
            Assert.That(f.World.Index.Find(id), Is.Null);
            Assert.That(f.World.Camp.CaptureState().Kills, Is.EqualTo(1));
            Assert.That(f.World.Economy.Stock.Gold, Is.EqualTo(2));
        });

        [UnityTest]
        public IEnumerator AttackWindupAndDoubleSpeedKeepSingleStepSemantics() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create(full: true);
            ActorBehaviour enemy = UnifiedGameplayProbe.Spawn(f.World, "zombie", 671);
            f.World.IssueOrders(new[] { 16 }, enemy.Id, enemy.X);
            f.World.SetTime(false, 2);
            f.World.Advance(1.0 / 60);
            ActorState attack = f.World.Index.Find<ActorBehaviour>(16).CaptureState();
            Assert.That(attack.Windup, Is.EqualTo(0.32));
            Assert.That(attack.HitPending, Is.True);
            Assert.That(f.World.Elapsed, Is.EqualTo(2.0 / 60));
            Assert.That(f.World.Waves.CaptureState().DayRemaining, Is.EqualTo(90 - 2.0 / 60));
            f.World.SetTime(false, 1);
            f.Step(19.0 / 60);
            Assert.That(enemy.Hp, Is.EqualTo(20));
            f.World.Advance(1.0 / 60);
            Assert.That(enemy.Hp, Is.InRange(13, 16));
            Assert.That(f.World.Index.Find<ActorBehaviour>(16).CaptureState().HitPending, Is.False);
        });

        [UnityTest]
        public IEnumerator TrainingFailureRollsBackWholeTickAndPartialPaymentRemainsOrdered() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create(full: true);
            JObject data = JObject.Parse(Save(f));
            data["world"]["economy"]["resources"]["food"] = 15;
            f.World.Restore(data.ToString());
            Assert.That(f.World.TrainActors("archer", new[] { 11, 12 }), Is.EqualTo(1));
            Assert.That(f.World.Economy.Stock.Food, Is.EqualTo(5));
            Assert.That(f.World.Index.Find<ActorBehaviour>(12).IsTraining, Is.False);
            while (f.World.Index.Find<ActorBehaviour>(11).Activity != ActorActivity.Training) f.World.Advance(1.0 / 60);
            data = JObject.Parse(Save(f));
            data["world"]["buildings"][2]["training_queue"][0]["remaining"] = 0.001;
            f.World.Restore(data.ToString());
            string before = Save(f);
            f.BreakArcher();
            try
            {
                Assert.Throws<InvalidOperationException>(() => f.World.Advance(1.0 / 60));
                Assert.That(Save(f), Is.EqualTo(before));
                Assert.That(f.World.Index.Find<ActorBehaviour>(11).RuleKey, Is.EqualTo("worker"));
            }
            finally { f.RepairArcher(); }
            f.World.Advance(1.0 / 60);
            Assert.That(f.World.Index.Find<ActorBehaviour>(11).RuleKey, Is.EqualTo("archer"));
        });

        [UnityTest]
        public IEnumerator ActiveWorldSaveRestoresTrainingConstructionFlightAndRandomContinuation() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create(true, true);
            f.World.TrainActors("archer", new[] { 13, 14 });
            f.World.PlaceBuilding("tower", 708, new[] { 11 });
            f.World.IssueOrders(new[] { 15 }, 4, 330);
            ActorBehaviour enemy = UnifiedGameplayProbe.Spawn(f.World, "zombie", 950);
            UnifiedGameplayProbe.Shoot(f.World, 620, enemy, 5);
            f.Step(1);
            string saved = Save(f);
            Assert.That(f.World.CaptureWorld().Projectiles.Count, Is.EqualTo(1));
            f.World.SetTime(true, 2);
            string paused = Save(f);
            f.Step(2);
            Assert.That(Save(f), Is.EqualTo(paused));
            f.World.Restore(saved);
            Assert.That(Save(f), Is.EqualTo(saved));
            f.Step(30);
            string expected = Save(f);
            f.World.Restore(saved);
            f.Step(30);
            Assert.That(Save(f), Is.EqualTo(expected));
        });

        private static string Save(UnifiedSliceFixture f) => f.World.SaveCodec.Serialize(f.World.CaptureWorld());
    }
}
