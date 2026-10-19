using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AnyRules.Next;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Entry;
using DarkNights.Entry.Terrain;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.View.Terrain;
using GameCore.Objects.Definition;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DarkNights.Tests
{
    /// <summary>真实Bootstrap／正式审核地图的投影回归。隔离存档，显式配发矿镐并布置站位后使用正式输入换区与挥镐；不证明完整徒步路线、多人或前台性能。</summary>
    public sealed class TerrainProjectionPlayTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private SessionNetwork network;
        private HeroPlayerController hero;
        private HeroInputPlayback input;
        private string failure;
        private readonly List<object> trace = new List<object>();
        private string output;
        private float? arrangedX;
        private TerrainPreview Preview => Object.FindAnyObjectByType<RandomLevelEntry>()?.Preview;
        private static EditorWindow Game => EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));

        [UnityTest]
        public IEnumerator FormalMapRefreshSurvivesMovementCollapseAndClearing()
        {
            EditorSceneManager.OpenScene(GameScenePaths.Bootstrap); Game.Focus();
            yield return new EnterPlayMode();
            yield return UniTask.ToCoroutine(Verify);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (network != null) network.Disconnect();
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        }

        private async UniTask Verify()
        {
            output = Path.GetFullPath("../artifacts/terrain-projection-validation-20261019/play-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            using var viewport = new TerrainGameViewTestSize(Game, 1280, 800);
            try
            {
                await Until(() => Object.FindAnyObjectByType<QuickTestHub>()?.Available == true, "Bootstrap", 90);
                network = Object.FindAnyObjectByType<SessionNetwork>();
                network.Failed += OnFailed;
                var storage = typeof(SessionNetwork).GetField("storage", Private).GetValue(network);
                storage.GetType().GetMethod("Initialize").Invoke(storage, new object[] { new[] { "--dn-save-dir", Path.Combine(output, "saves") } });
                var flow = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch").SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
                await network.Connect(true, "127.0.0.1", 27919, QuickTestPreset.Create(QuickTestPreset.ShipCargoId, flow));
                hero = network.GetComponent<HeroPlayerController>();
                typeof(HeroPlayerController).GetField("replayOnly", Private).SetValue(hero, true);
                input = network.gameObject.AddComponent<HeroInputPlayback>(); input.Initialize(network);
                await Until(() => hero.Current != null && network.Client.Ready && Preview?.Ready == true, "initial Ready", 150);
                PreparePickaxe();
                await Until(() => Tool() != null, "fixture pickaxe publication", 15);
                var events = (IEnumerable<GeologyEventBehaviour>)typeof(SessionGeology).GetField("events", Private).GetValue(network.ObjectWorld.Geology);
                var geology = events.Single(); var config = geology.Configuration;
                Assert.That(geology.Capture().Phase, Is.Zero);
                Mark("initial");

                float boundary = 96 * 16;
                Pose(boundary - 40, Ground(boundary - 40, 720, -200));
                await Stable("before input crossing");
                ulong generation = network.Terrain.Replica.Generation;
                await Hold(1); await Until(() => hero.Current.X > boundary + 24, "trusted walk crosses chunk", 15);
                await Hold(0); await Stable("after input crossing");
                Assert.That(network.Terrain.Replica.Generation, Is.GreaterThan(generation));
                await Hold(-1); await Until(() => hero.Current.X < boundary - 24, "trusted return crosses chunk", 15);
                await Hold(0); await Stable("after return crossing");
                Mark("normal input round trip");

                for (int pass = 0; pass < 3; pass++)
                {
                    ulong before = network.Terrain.Replica.Generation;
                    Pose(250 * 16, Ground(250 * 16, 720, -200));
                    await Until(() => network.Terrain.Replica.Generation > before, "far stream generation", 15);
                    await Stable("far region " + pass); Mark("far " + pass + " source=" + config.Source(0));
                    Assert.That(network.Terrain.Replica.Read(config.Source(0)).State, Is.EqualTo(GridSampleState.Unknown));
                    before = network.Terrain.Replica.Generation;
                    Pose(144 * 16, Ground(144 * 16, 720, -200));
                    await Until(() => network.Terrain.Replica.Generation > before, "near stream generation", 15);
                    await Stable("near region " + pass);
                    Assert.That(network.Terrain.Replica.Read(config.Source(0)).TryGetCell(out _), Is.True);
                    Mark("known/unknown round trip " + pass);
                }

                float triggerX = (config.RegionU + 1) * 16;
                Pose(triggerX, Ground(triggerX, TerrainMiningGeometry.CenterHeight(config.RegionV) + 32,
                    TerrainMiningGeometry.CenterHeight(config.RegionV) - 32));
                await Until(() => geology.Capture().Phase == 1, "formal region warning", 10);
                await Until(() => geology.Capture().Phase == 4, "formal collapse landing", 20);
                await Stable("collapse projection"); Mark("formal landed");
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "landed.png"));
                await UniTask.Yield();
                var map = network.ObjectWorld.Terrain.Map;
                for (int i = 0; i < config.RockCount; i++) Assert.That(map.Read(config.Source(i)).Cell.IsEmpty, Is.True);
                int actions = 0;
                while (Enumerable.Range(0, config.RubbleCount).Any(i => !map.Read(config.Landing(i)).Cell.IsEmpty))
                {
                    Assert.That(actions++, Is.LessThan(config.RubbleCount * 2), "清障须有界且每次真实采空一格。");
                    var tool = Tool();
                    Assert.That(tool, Is.Not.Null);
                    bool found = false; CellCoord target = default; float aim = 0;
                    foreach (var index in Enumerable.Range(0, config.RubbleCount))
                    {
                        target = config.Landing(index); if (map.Read(target).Cell.IsEmpty) continue;
                        foreach (int offset in new[] { 0, -1, 1, -2, 2, -3, 3, -4, 4 })
                        {
                            float x = (target.U + offset) * 16;
                            float y;
                            try { y = Ground(x, TerrainMiningGeometry.CenterHeight(target.V) + 32,
                                TerrainMiningGeometry.CenterHeight(target.V) - 128); }
                            catch (AssertionException) { continue; }
                            float dh = TerrainMiningGeometry.CenterHeight(target.V) - y - tool.HandHeight;
                            float dx = target.U * 16 - x;
                            float angle = (float)(Math.Atan2(dh, dx) * 180 / Math.PI);
                            float radians = angle * (float)Math.PI / 180;
                            if (!DarkNights.Runtime.Terrain.TerrainMiningQuery.FirstSurface(map, x, y + tool.HandHeight,
                                (float)Math.Cos(radians), (float)Math.Sin(radians), tool.Reach, out var first, out _) || !first.Equals(target)) continue;
                            Pose(x, y); aim = angle; found = true; break;
                        }
                        if (found) break;
                    }
                    Assert.That(found, Is.True, "未找到可以通过真实射线采掘的瓦砾边缘，不绕过身体或矿镐规则。");
                    await Stable("mining stance");
                    await Hold(0, true, aim, target);
                    await Until(() => map.Read(target).Cell.IsEmpty, "formal pickaxe " + target, 15);
                    await Hold(0); await Stable("mined projection");
                    Mark("mined " + target);
                }
                await Until(() => geology.Capture().Phase == 5, "cleared phase", 10);
                await Stable("cleared projection"); Mark("formal cleared");
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "cleared.png")); await UniTask.Yield();
                Assert.That(failure, Is.Null);
            }
            catch (Exception error) { failure = error.ToString(); throw; }
            finally
            {
                File.WriteAllText(Path.Combine(output, "trace.json"), JsonConvert.SerializeObject(new { failure, trace }, Formatting.Indented));
                if (network != null) network.Failed -= OnFailed;
                Application.runInBackground = background;
            }
        }

        private void OnFailed(Exception error) { failure = error.ToString(); }
        private MiningToolRules Tool()
        {
            var slots = new[] { hero.Current.Slot0Definition, hero.Current.Slot1Definition, hero.Current.Slot2Definition, hero.Current.Slot3Definition };
            return network.ObjectWorld.Resources.Equipment.Mining(slots[hero.Current.SelectedItem]);
        }
        private void PreparePickaxe()
        {
            var actor = network.ObjectWorld.Index.Find<ActorBehaviour>(hero.Current.Id);
            string guid = network.ObjectWorld.Resources.Equipment.MiningDefinitions.Single(d => d.Key == "item.pickaxe").Guid.ToString();
            network.ObjectWorld.Mutations.Run(() =>
            {
                var state = (ActorState)typeof(SessionStateBehaviour<ActorState>).GetMethod("Edit", Private).Invoke(actor, null);
                Assert.That((bool)typeof(HeroInventoryBehaviour).GetMethod("Give", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { state, guid }), Is.True);
                int slot = Enumerable.Range(0, 4).Single(i => HeroInventoryBehaviour.Slot(state, i) == guid);
                typeof(ActorState).GetProperty("SelectedItem").SetValue(state, slot); return true;
            });
        }
        private async UniTask Stable(string reason)
        {
            if (arrangedX.HasValue)
            {
                float x = arrangedX.Value; arrangedX = null;
                await Until(() => Math.Abs(hero.Current.X - x) < 4, reason + " pose publication", 15);
            }
            await UniTask.Yield(); await UniTask.Yield();
            await Until(() => network.Terrain.LocalDataReady && Preview?.Ready == true && !Preview.RefreshingReplica &&
                network.Terrain.Region.Equals(DarkNights.Runtime.Terrain.SessionMapRegion.Client(network)) &&
                Preview.LightingLoadedBounds.Equals(network.Terrain.Region) &&
                Preview.InstalledInputGeneration == Preview.PresentedInputGeneration &&
                Preview.InstalledSourceCommit == Preview.PresentedSourceCommit, reason, 60);
        }
        private async UniTask Until(Func<bool> ready, string reason, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!ready())
            {
                Assert.That(failure, Is.Null, reason);
                Assert.That(input?.Error, Is.Null, reason);
                Assert.That(Preview?.LastError, Is.Null, reason);
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), reason + ": " + Preview?.PresentationWaitReason);
                Game.Repaint(); EditorApplication.QueuePlayerLoopUpdate(); await UniTask.Yield();
            }
        }
        private async UniTask Hold(int horizontal, bool use = false, float aim = 0, CellCoord target = default)
        {
            var command = new JObject { ["operation"] = "input-hold", ["actor"] = hero.Current.Id,
                ["horizontal"] = horizontal, ["useHeld"] = use, ["aimAngle"] = aim };
            if (use) command["mining"] = new JObject { ["u"] = target.U, ["v"] = target.V };
            await input.Execute(command);
        }
        private void Pose(float x, float height)
        {
            arrangedX = x;
            var actor = network.ObjectWorld.Index.Find<ActorBehaviour>(hero.Current.Id);
            network.ObjectWorld.Mutations.Run(() =>
            {
                var state = (ActorState)typeof(SessionStateBehaviour<ActorState>).GetMethod("Edit", Private).Invoke(actor, null);
                void Set(string name, object value) => typeof(ActorState).GetProperty(name).SetValue(state, value);
                Set("X", x); Set("Height", height); Set("VerticalSpeed", 0f); Set("SupportPlatform", 0);
                Set("Boarded", false); Set("ShipEntryBlocked", true); Set("Horizontal", 0);
                return true;
            });
        }
        private float Ground(float x, float high, float low)
        {
            var collision = typeof(DarkNights.Runtime.Terrain.TerrainMiningQuery).Assembly.GetType("DarkNights.Runtime.Terrain.TerrainBodyCollision");
            var arguments = new object[] { network.ObjectWorld.Terrain.Map, x, high, low,
                HeroControlDefinition.BodyHalfWidth, HeroControlDefinition.BodyHeight, 0f };
            Assert.That((bool)collision.GetMethod("Ground").Invoke(null, arguments), Is.True, "无安全身体站位：" + x + ", " + high + ".." + low);
            return (float)arguments[6];
        }
        private void Mark(string name)
        {
            trace.Add(new { name, x = hero.Current.X, height = hero.Current.Height, region = network.Terrain.Region,
                stream = network.Terrain.Replica.Generation, commit = network.Terrain.Replica.CommitId,
                installed = Preview.InstalledInputGeneration, presented = Preview.PresentedInputGeneration,
                visualRevision = Preview.VisualRevision, ready = Preview.Ready });
        }
    }
}
