using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Entry;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using DarkNights.View;
using DarkNights.View.Terrain;
using GameCore.Objects.Definition;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>本次地图合同的有限真实Bootstrap验收；检查快速局、历史存档及新图开局，配置变化仅限运行副本，存档写隔离目录。</summary>
    [Explicit("须先完成编辑态保存夹具，并准备本批保全的历史地图与v27存档；仅按用例过滤器显式执行。")]
    public sealed class MapContractRuntimeAcceptanceTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static string Output => MapContractRuntimeFixture.Output;
        private static async UniTask Until(Func<bool> condition, float seconds, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), message);
                EditorApplication.QueuePlayerLoopUpdate();
                await UniTask.Yield();
            }
        }
        private static async UniTask Ready(SessionNetwork network, string name)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 90;
            double reportAt = 0;
            var target = new RenderTexture(1280, 720, 24);
            target.Create();
            try
            {
                while (!network.Client.Ready)
                {
                    Assert.That(network.CanStartSession, Is.False, name + "连接已结束：" + network.Status);
                    var stage = UnityEngine.Object.FindAnyObjectByType<PinewatchStage>();
                    if (stage?.SceneCamera != null)
                    {
                        var camera = stage.SceneCamera;
                        var previous = camera.targetTexture;
                        try { camera.targetTexture = target; camera.Render(); }
                        finally { camera.targetTexture = previous; }
                    }
                    if (Time.realtimeSinceStartupAsDouble >= reportAt)
                    {
                        reportAt = Time.realtimeSinceStartupAsDouble + 10;
                        var terrain = network.Terrain;
                        File.AppendAllText(Path.Combine(Output, "ready-diagnostics.txt"), name + " " +
                            new JObject
                            {
                                ["status"] = network.Status, ["client"] = network.Client.Status, ["data"] = terrain.DataReady,
                                ["local"] = terrain.LocalDataReady, ["visible"] = terrain.PresentationReady,
                                ["frame"] = network.Client.Replica.Current != null, ["epoch"] = terrain.Epoch,
                                ["presentation"] = UnityEngine.Object.FindAnyObjectByType<TerrainPreview>()?.PresentationWaitReason,
                                ["details"] = string.Join("; ", terrain.Capture().Select(p => p.Key + "=" + p.Value))
                            }.ToString(Newtonsoft.Json.Formatting.None) + System.Environment.NewLine);
                    }
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), name + "未Ready：" + network.Status);
                    EditorApplication.QueuePlayerLoopUpdate();
                    await UniTask.Yield();
                }
            }
            finally { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        [UnityTest]
        public IEnumerator PoolSeedsLaunchBothQuickSessionsWithoutChangingAuthoring()
        {
            EditorSceneManager.OpenScene(GameScenePaths.Bootstrap);
            SessionState.SetString(MapContractRuntimeFixture.ModeKey, "pool");
            yield return new EnterPlayMode();
            yield return UniTask.ToCoroutine(QuickSessions);
            yield return new ExitPlayMode();
        }
        private static async UniTask QuickSessions()
        {
            Application.runInBackground = true;
            await Until(() => UnityEngine.Object.FindAnyObjectByType<QuickTestHub>()?.Available == true, 90, "Bootstrap未就绪");
            var network = UnityEngine.Object.FindAnyObjectByType<SessionNetwork>();
            var source = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch");
            int index = source.SharedConfigs.FindIndex(x => x is ExpeditionFlowConfig);
            var flow = (ExpeditionFlowConfig)source.SharedConfigs[index];
            Assert.That(flow.CaveMap.SeedSelection.Mode, Is.EqualTo(TerrainSeedSettings.Pool));
            string before = JsonUtility.ToJson(flow.CaveMap);
            var storage = typeof(SessionNetwork).GetField("storage", Flags).GetValue(network);
            storage.GetType().GetMethod("Initialize").Invoke(storage, new object[]
                { new[] { "--dn-save-dir", Path.Combine(Output, "quick-saves") } });
            var controller = UnityEngine.Object.FindAnyObjectByType<HeroPlayerController>();
            bool inputEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
            try
            {
                foreach (string id in new[] { QuickTestPreset.LandedPickaxeId, QuickTestPreset.EmbeddedMineralsId })
                {
                    var preset = QuickTestPreset.Create(id, flow);
                    Assert.That(flow.CaveMap.SeedSelection.Seeds.Contains(preset.Seed), Is.False);
                    await network.Connect(true, "127.0.0.1", 28031, preset);
                    await Ready(network, id);
                    Assert.That(network.Client.Replica.Current.World.Expedition.Journey, Is.Null);
                    Assert.That(network.Client.Replica.Current.World.Expedition.Ship, Is.Null);
                    Assert.That(network.ObjectWorld.Flow.Phase, Is.EqualTo(JourneyPhase.Landed));
                    Assert.That(network.ObjectWorld.Index.Buildings.Count(), Is.Zero);
                    Assert.That(network.ObjectWorld.Terrain.Seed, Is.EqualTo(preset.Seed));
                    Assert.That(JsonUtility.ToJson(flow.CaveMap), Is.EqualTo(before));
                    if (id == QuickTestPreset.EmbeddedMineralsId &&
                        !string.IsNullOrEmpty(SessionState.GetString("DarkNights.MapFlow.Output", "")))
                        await MapFlowMiningAcceptance.Check(network, Output);
                    TestContext.WriteLine("Ready under authoring pool: " + id + ", seed=" + preset.Seed);
                    network.Disconnect();
                    await Until(() => network.CanStartSession, 30, "快速局未退出");
                    await UniTask.DelayFrame(3);
                }
            }
            finally
            {
                if (controller != null) controller.enabled = inputEnabled;
                network.Disconnect(); MapContractRuntimeFixture.Restore();
            }
        }
        [UnityTest]
        public IEnumerator HistoricalV27SaveLoadsWithItsOriginalMapConfiguration()
        {
            EditorSceneManager.OpenScene(GameScenePaths.Bootstrap);
            SessionState.SetString(MapContractRuntimeFixture.ModeKey, "historical");
            yield return new EnterPlayMode();
            yield return UniTask.ToCoroutine(HistoricalSave);
            yield return new ExitPlayMode();
        }
        private static async UniTask HistoricalSave()
        {
            await Until(() => UnityEngine.Object.FindAnyObjectByType<QuickTestHub>()?.Available == true, 90, "Bootstrap未就绪");
            var network = UnityEngine.Object.FindAnyObjectByType<SessionNetwork>();
            var source = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch");
            int index = source.SharedConfigs.FindIndex(x => x is ExpeditionFlowConfig);
            var flow = (ExpeditionFlowConfig)source.SharedConfigs[index];
            Assert.That(flow.CaveMap.Seed, Is.EqualTo("CL-REVIEW-20261022-A"));
            string path = Path.GetFullPath("../artifacts/local-menu-multiplayer-20261023/runs/" +
                "network-20261023-043108-306705-mono-normal-2p-client/saves/v27/slot-00.dnsave.json");
            string json = File.ReadAllText(path);
            var expected = JObject.Parse(json);
            try
            {
                var storage = typeof(SessionNetwork).GetField("storage", Flags).GetValue(network);
                storage.GetType().GetMethod("Initialize").Invoke(storage, new object[]
                    { new[] { "--dn-save-dir", Path.Combine(Output, "historical-save-isolated") } });
                await network.Connect(true, "127.0.0.1", 28032);
                await Ready(network, "historical");
                Assert.That(network.ObjectWorld.Flow.ContentFingerprint,
                    Is.EqualTo((string)expected["world"]["expedition"]["Journey"]["ContentFingerprint"]));
                network.ObjectWorld.Restore(json);
                var restored = JObject.Parse(network.ObjectWorld.SaveCodec.Serialize(network.ObjectWorld.CaptureWorld()));
                Assert.That(JToken.DeepEquals(restored["world"]["terrain"], expected["world"]["terrain"]), Is.True,
                    "历史最终地形／矿层／事件状态不得由新规则补回");
                Assert.That(restored["format_version"], Is.EqualTo(expected["format_version"]));
                TestContext.WriteLine("Read original v27 save: " + path);
            }
            finally
            {
                network.Disconnect(); MapContractRuntimeFixture.Restore();
            }
        }
        [UnityTest]
        public IEnumerator SavedMapStartsNormalNewSessionFromInstalledDocument()
        {
            EditorSceneManager.OpenScene(GameScenePaths.Bootstrap);
            SessionState.SetString(MapContractRuntimeFixture.ModeKey, "saved");
            yield return new EnterPlayMode();
            yield return UniTask.ToCoroutine(SavedMapSession);
            yield return new ExitPlayMode();
        }
        private static async UniTask SavedMapSession()
        {
            await Until(() => UnityEngine.Object.FindAnyObjectByType<QuickTestHub>()?.Available == true, 90, "Bootstrap未就绪");
            var network = UnityEngine.Object.FindAnyObjectByType<SessionNetwork>();
            var flow = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch")
                .SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            Assert.That(flow.ReviewedMap.HasHistoricalSource, Is.False);
            Assert.That(flow.CaveMap.SeedSelection.Mode, Is.EqualTo(TerrainSeedSettings.Pool));
            try
            {
                var storage = typeof(SessionNetwork).GetField("storage", Flags).GetValue(network);
                storage.GetType().GetMethod("Initialize").Invoke(storage, new object[]
                    { new[] { "--dn-save-dir", Path.Combine(Output, "saved-map-new-session") } });
                await network.Connect(true, "127.0.0.1", 28033);
                await Ready(network, "saved-map-new-session");
                Assert.That(network.ObjectWorld.Flow.Phase, Is.EqualTo(JourneyPhase.Landed));
                var actual = network.ObjectWorld.Terrain.Capture();
                var expected = flow.ReviewedMap.Freeze(actual.WorldId);
                Assert.That(actual.Seed, Is.EqualTo(expected.Seed));
                Assert.That(actual.Background.ReferenceHash, Is.EqualTo(expected.Background.ReferenceHash));
                CollectionAssert.AreEqual(expected.CopyMaterials(), actual.CopyMaterials());
                CollectionAssert.AreEqual(expected.CopyShapes(), actual.CopyShapes());
                var beforeSave = network.ObjectWorld.CaptureWorld();
                Assert.That(beforeSave.Expedition.Ship, Is.Null);
                Assert.That(beforeSave.Expedition.Journey, Is.Null);
                string saved = network.ObjectWorld.SaveCodec.Serialize(beforeSave);
                File.WriteAllText(Path.Combine(Output, "shipless-new-session.dnsave.json"), saved);
                network.ObjectWorld.Restore(saved);
                var reopened = JObject.Parse(network.ObjectWorld.SaveCodec.Serialize(network.ObjectWorld.CaptureWorld()));
                var stored = JObject.Parse(saved);
                Assert.That(JToken.DeepEquals(reopened["world"]["terrain"], stored["world"]["terrain"]), Is.True);
                Assert.That(network.ObjectWorld.Flow.Phase, Is.EqualTo(JourneyPhase.Landed));
                Assert.That(network.ObjectWorld.Index.Buildings.Count(), Is.Zero);
                TestContext.WriteLine("Normal new session uses saved map, seed=" + actual.Seed);
            }
            finally { network.Disconnect(); MapContractRuntimeFixture.Restore(); }
        }
        [UnityTearDown]
        public IEnumerator ExitPlayAfterFailure()
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.FindAnyObjectByType<SessionNetwork>()?.Disconnect();
                MapContractRuntimeFixture.Restore();
                yield return new ExitPlayMode();
            }
        }
    }
}
