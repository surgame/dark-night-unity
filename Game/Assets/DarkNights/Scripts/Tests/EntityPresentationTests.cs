using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using DarkNights.Editor;
using DarkNights.Runtime.Config;
using DarkNights.View;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>
    /// 用真实原生 Prefab 和生成绑定检验个体身份、冻结状态解释、显式操作和生命周期边界。
    /// 测试只装配临时表现，不启动网络、模拟或存档；失效更新不得改变显示或复活旧副本。
    /// </summary>
    public sealed class EntityPresentationTests
    {
        private readonly List<(GameObject Root, EntityPresentationBehaviour Behaviour)> loaded =
            new List<(GameObject, EntityPresentationBehaviour)>();
        private GameCatalog catalog;

        [SetUp]
        public void ReadRules() => catalog = GameCatalogJson.Parse(
            File.ReadAllText(GameContentSetup.ConfigRoot + "balance.json"), File.ReadAllText(GameContentSetup.ConfigRoot + "pinewatch.json"));

        [TearDown]
        public void Release()
        {
            foreach (var item in loaded) { item.Behaviour.Dispose(); PrefabUtility.UnloadPrefabContents(item.Root); }
            loaded.Clear();
        }

        [Test]
        public void BindDespawnAndReentryClearStateIdentityAndCallbacks()
        {
            ActorPresentationBehaviour actor = Load<ActorPresentationBehaviour>("Worker");
            ActorView visual = (ActorView)actor.Visual;
            ActorViewData state = Actor("worker", "Idle");
            Assert.That(actor.IsBound || actor.IsAvailable, Is.False);
            Assert.That(actor.Present(state, 2, "", 100, 0, Color.white), Is.False);
            visual.Preview(0, 0);
            Assert.That(actor.Current, Is.Null);
            int oldCalls = 0, newCalls = 0;
            actor.Bind(7, 2, "worker", catalog.Balance.Units["worker"], _ => oldCalls++);
            Assert.That(actor.IssueOrder(9, 200), Is.False, "Binding alone is not a received live state.");
            Assert.That(actor.Present(state, 2, "", 100, 0, Color.white), Is.True);
            Assert.That(actor.Current, Is.SameAs(state));
            Assert.That(actor.MaxHp, Is.EqualTo(catalog.Balance.Units["worker"].Hp));
            Assert.That(actor.IssueOrder(9, 200), Is.True);
            Assert.That(actor.Present(Actor("worker", "Work", id: 8), 2, "wood", 900, 0, Color.red), Is.False);
            Assert.That(actor.Present(Actor("spearman", "Idle"), 2, "", 900, 0, Color.red), Is.False);
            Assert.That(actor.Present(state, 1, "", 900, 0, Color.red), Is.False);
            Assert.That(visual.transform.position.x, Is.EqualTo(1));
            Assert.That(actor.Current, Is.SameAs(state));
            actor.Unbind();
            Assert.That(actor.Current, Is.Null);
            Assert.That(actor.Id + actor.Epoch + actor.MaxHp, Is.Zero);
            Assert.That(actor.IssueOrder(9, 200), Is.False);
            for (int epoch = 3; epoch < 6; epoch++)
            {
                actor.Bind(7, epoch, "worker", catalog.Balance.Units["worker"], _ => newCalls++);
                Assert.That(actor.Present(state, epoch, "", 100, 0, Color.white), Is.True);
                Assert.That(actor.Train("spearman"), Is.True);
                actor.OnDespawn();
                Assert.That(actor.Current, Is.Null);
                Assert.That(actor.Visual, Is.Null);
                Assert.That(actor.Present(state, epoch, "", 900, 0, Color.red), Is.False);
                actor.Initialize(new BehaviourContext(loaded[0].Root.GetComponent<ObjectInstance>(), null));
                Assert.That(actor.Visual, Is.SameAs(visual));
                Assert.That(actor.IsBound || actor.IsAvailable, Is.False);
            }
            Assert.That(oldCalls, Is.EqualTo(1));
            Assert.That(newCalls, Is.EqualTo(3));
        }

        [Test]
        public void ProfessionReplacementKeepsIdentityButRejectsOldVisualUpdates()
        {
            var worker = Load<ActorPresentationBehaviour>("Worker");
            var guard = Load<ActorPresentationBehaviour>("Spearman");
            int oldCalls = 0, newCalls = 0;
            worker.Bind(7, 2, "worker", catalog.Balance.Units["worker"], _ => oldCalls++);
            worker.Present(Actor("worker", "Training"), 2, "", 100, 0, Color.white);
            worker.Unbind();
            guard.Bind(7, 2, "spearman", catalog.Balance.Units["spearman"], _ => newCalls++);
            guard.Present(Actor("spearman", "Idle"), 2, "", 100, 0, Color.white);
            Assert.That(worker.Present(Actor("worker", "Move"), 2, "", 200, 1, Color.white), Is.False);
            Assert.That(worker.IssueOrder(31, 200), Is.False);
            Assert.That(guard.IssueOrder(31, 200), Is.True);
            worker.OnDespawn();
            Assert.That(worker.Current, Is.Null);
            Assert.That(guard.Id, Is.EqualTo(7));
            Assert.That(guard.Current.Kind, Is.EqualTo("spearman"));
            Assert.That(oldCalls, Is.Zero);
            Assert.That(newCalls, Is.EqualTo(1));
        }

        [TestCase("Move", "", "move")]
        [TestCase("WorkMove", "wood", "move")]
        [TestCase("BuildMove", "", "move")]
        [TestCase("TrainingMove", "", "move")]
        [TestCase("Work", "wood", "work_wood")]
        [TestCase("Work", "food", "work_farm")]
        [TestCase("Work", "stone", "work_mine")]
        [TestCase("Work", "iron", "work_mine")]
        [TestCase("Build", "", "build")]
        [TestCase("Training", "", "idle")]
        public void WorkerOwnsPoseAndExplicitActorOperations(string activity, string work, string pose)
        {
            var actor = Load<ActorPresentationBehaviour>("Worker");
            var intents = new List<InputIntent>();
            actor.Bind(7, 2, "worker", catalog.Balance.Units["worker"], intents.Add);
            ActorViewData state = Actor("worker", activity);
            actor.Present(state, 2, work, 250, 0.25, Color.white);
            Assert.That(actor.Pose, Is.EqualTo(pose));
            Assert.That(actor.Current.Hp, Is.EqualTo(25));
            Assert.That(actor.Visual.transform.position.x, Is.EqualTo(2.5));
            Assert.That(((ActorView)actor.Visual).Clips.Any(value => value.Name == pose), Is.True);
            actor.IssueOrder(31, 450);
            actor.Train("archer");
            Assert.That(intents[0].Actors, Is.EqualTo(new[] { 7 }));
            Assert.That(intents[0].Target, Is.EqualTo(31));
            Assert.That(intents[0].X, Is.EqualTo(450));
            Assert.That(intents[1].Action, Is.EqualTo("Trainarcher"));
            Assert.That(intents[1].Actors, Is.EqualTo(new[] { 7 }));
            Assert.That(state.Hp, Is.EqualTo(25), "Local operations cannot settle damage or training.");
        }

        [TestCase("Worker")]
        [TestCase("Spearman")]
        [TestCase("Archer")]
        [TestCase("Zombie")]
        [TestCase("Ghoul")]
        [TestCase("Armored")]
        public void AttackUsesSharedDisplayTimeMappedToOriginalClip(string name)
        {
            string kind = name.ToLowerInvariant();
            var actor = Load<ActorPresentationBehaviour>(name);
            var expected = Load<ActorPresentationBehaviour>(name);
            actor.Bind(7, 2, kind, catalog.Balance.Units[kind], null);
            double time = catalog.Balance.Units[kind].AttackSeconds * 0.6;
            actor.Present(Actor(kind, "Attack"), 2, "", 300, time, Color.white);
            var expectedView = (ActorView)expected.Visual;
            expectedView.SamplePose("attack", expectedView.PoseDuration("attack") * 0.6);
            var actualSprites = actor.Visual.GetComponentsInChildren<SpriteRenderer>(true);
            var expectedSprites = expected.Visual.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(actualSprites.Length, Is.EqualTo(expectedSprites.Length));
            for (int i = 0; i < actualSprites.Length; i++)
            {
                Assert.That(actualSprites[i].sprite, Is.SameAs(expectedSprites[i].sprite), name + " sprite " + i);
                Assert.That(actualSprites[i].enabled, Is.EqualTo(expectedSprites[i].enabled), name + " visibility " + i);
                Assert.That(actualSprites[i].transform.localPosition, Is.EqualTo(expectedSprites[i].transform.localPosition));
            }
        }

        [TestCase("House")]
        [TestCase("Tavern")]
        [TestCase("Barracks")]
        [TestCase("Farm")]
        [TestCase("Tower")]
        public void BuildingOwnsConstructionAndPreservesFrozenTraining(string name)
        {
            string kind = name.ToLowerInvariant();
            var building = Load<BuildingPresentationBehaviour>(name);
            InputIntent submitted = null;
            building.Bind(11, 2, kind, value => submitted = value);
            var training = new[] { new TrainingViewData(7, "spearman", 3) };
            var state = new BuildingViewData(11, kind, 400, 80, 0.5, 7, 0, 0, training);
            Assert.That(building.Present(state, 2, Color.white), Is.True);
            Assert.That(building.IsConstructing, Is.True);
            Assert.That(building.TrainingCount, Is.EqualTo(1));
            Assert.That(building.Current.Training[0].Remaining, Is.EqualTo(3));
            var buildingView = (BuildingView)building.Visual;
            var fields = new SerializedObject(buildingView);
            var complete = (SpriteRenderer)fields.FindProperty("complete").objectReferenceValue;
            Assert.That(complete.enabled, Is.EqualTo(buildingView.FadeConstruction));
            if (buildingView.FadeConstruction) Assert.That(complete.color.a, Is.EqualTo(0.7).Within(0.00001));
            var finished = new BuildingViewData(11, kind, 400, 80, 1, 0, 0, 0, training);
            building.Present(finished, 2, Color.white);
            Assert.That(building.IsConstructing, Is.False);
            Assert.That(complete.enabled, Is.True);
            Assert.That(building.Repair(), Is.True);
            Assert.That(submitted.Action, Is.EqualTo("Repair"));
            Assert.That(submitted.Target, Is.EqualTo(11));
            Assert.That(submitted.Actors, Is.Empty);
            building.Unbind();
            Assert.That(building.Repair(), Is.False);
            Assert.That(building.Current, Is.Null);
        }

        [TestCase("Trees", "wood")]
        [TestCase("Stone", "stone")]
        [TestCase("Iron", "iron")]
        [TestCase("Farmland", "food")]
        public void WorksiteOwnsVariantDepletionAndExplicitTarget(string name, string kind)
        {
            var site = Load<WorksitePresentationBehaviour>(name);
            InputIntent submitted = null;
            site.Bind(31, 2, kind, value => submitted = value);
            var state = new WorksiteViewData(31, kind, 500, 7, 25, 0.3, 1, 0);
            site.Present(state, 2, Color.white);
            var fields = new SerializedObject(site.Visual);
            var depleted = (GameObject)fields.FindProperty("depleted").objectReferenceValue;
            var variants = fields.FindProperty("variants");
            Assert.That(depleted.activeSelf, Is.False);
            for (int i = 0; i < variants.arraySize; i++)
                Assert.That(((SpriteRenderer)variants.GetArrayElementAtIndex(i).objectReferenceValue).enabled, Is.EqualTo(i == 1 % variants.arraySize));
            int[] selected = { 7, 8 };
            site.AssignWorkers(selected);
            selected[0] = 99;
            Assert.That(submitted.Actors, Is.EqualTo(new[] { 7, 8 }));
            Assert.That(submitted.Target, Is.EqualTo(31));
            site.Present(new WorksiteViewData(31, kind, 500, 0, 0, 0, 1, 0), 2, Color.white);
            Assert.That(site.IsDepleted && depleted.activeSelf, Is.True);
            site.Present(new WorksiteViewData(31, kind, 500, 0, 0, 0, 1, 11), 2, Color.white);
            Assert.That(depleted.activeSelf, Is.False, "A farm-linked site does not draw a second ground marker.");
            site.Unbind();
            Assert.That(site.Current, Is.Null);
            Assert.That(site.AssignWorkers(selected), Is.False);
        }

        [Test]
        public void TimelinePauseAndSpeedStillDriveTheSameIndividual()
        {
            var actor = Load<ActorPresentationBehaviour>("Worker");
            actor.Bind(7, 2, "worker", catalog.Balance.Units["worker"], null);
            var timeline = new PresentationTimeline();
            ActorViewData first = Actor("worker", "Move", x: 100, actionTime: 1);
            ActorViewData second = Actor("worker", "Move", x: 200, actionTime: 1.2);
            timeline.Push(Frame(first, 1, 0, false, 2), 0);
            timeline.Push(Frame(second, 2, 6, false, 2), 0.1);
            actor.Present(second, 2, "", timeline.X(second, 0.15), timeline.ActionTime(second, 0.15), Color.white);
            Assert.That(actor.Visual.transform.position.x, Is.EqualTo(1.5).Within(0.00001));
            Assert.That(timeline.ActionTime(second, 0.15), Is.EqualTo(1.1).Within(0.00001));
            timeline.Push(Frame(second, 3, 6, true, 2), 0.15);
            actor.Present(second, 2, "", timeline.X(second, 100), timeline.ActionTime(second, 100), Color.white);
            Assert.That(actor.Visual.transform.position.x, Is.EqualTo(2));
            Assert.That(timeline.ActionTime(second, 100), Is.EqualTo(1.2));
        }

        private T Load<T>(string name) where T : EntityPresentationBehaviour, new()
        {
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/DarkNights/Res/Objects/" + name + "/" + name + ".prefab");
            var value = new T();
            loaded.Add((root, value));
            value.Initialize(new BehaviourContext(root.GetComponent<ObjectInstance>(), null));
            Assert.That(value.Visual, Is.Not.Null, "Generated inherited visual binding: " + name);
            return value;
        }

        private static ActorViewData Actor(string kind, string activity, int id = 7, float x = 100, double actionTime = 0) =>
            new ActorViewData(id, kind, "Test", false, x, 25, activity, 31, 1, false, actionTime, 0, 0);

        private static SessionViewData Frame(ActorViewData actor, long publication, long tick, bool paused, int speed)
        {
            var camp = new CampViewData(new ResourceAmounts(), 1, 10, 0, 0, "Day", 100, 0, "Playing", 0, 0, new ResourceAmounts());
            var world = new WorldViewData(camp, new[] { actor }, Array.Empty<BuildingViewData>(), Array.Empty<WorksiteViewData>(), Array.Empty<ProjectileViewData>());
            return new SessionViewData(publication, 2, 0, tick, 0, false, 1, 1, false, paused, speed, tick / 60.0, world);
        }
    }
}
