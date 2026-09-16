using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Entry;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.View;
using GameCore.Objects.Runner;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 使用真实 Bootstrap、Pinewatch、网络会话工厂和本地 Host 验证新版正式装配链。
    /// 开关域重载各重复两次 Play，场景原实例、统一状态所有权与退出撤权必须同时成立。
    /// </summary>
    [Category("UnifiedPlay")]
    public sealed class UnifiedSlicePlayTests
    {
        private const string SettingsKey = "DarkNights.UnifiedSlicePlay.Settings";
        private const string SceneKey = "DarkNights.UnifiedSlicePlay.Scene";

        [UnityTest]
        public IEnumerator FormalPlayWithDomainReload() => RepeatPlay(false);

        [UnityTest]
        public IEnumerator FormalPlayWithoutDomainReload() => RepeatPlay(true);

        [UnityTearDown]
        public IEnumerator RestoreEditor()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            int settings = SessionState.GetInt(SettingsKey, -1);
            if (settings < 0) yield break;
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)(settings >> 1);
            EditorSettings.enterPlayModeOptionsEnabled = (settings & 1) != 0;
            string scene = SessionState.GetString(SceneKey, "");
            if (!string.IsNullOrEmpty(scene)) EditorSceneManager.OpenScene(scene);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.EraseInt(SettingsKey);
            SessionState.EraseString(SceneKey);
        }

        private static IEnumerator RepeatPlay(bool disableReload)
        {
            if (SessionState.GetInt(SettingsKey, -1) < 0)
            {
                SessionState.SetInt(SettingsKey, ((int)EditorSettings.enterPlayModeOptions << 1) |
                    (EditorSettings.enterPlayModeOptionsEnabled ? 1 : 0));
                var scene = EditorSceneManager.GetActiveScene();
                SessionState.SetString(SceneKey, scene.path);
                if (!string.IsNullOrEmpty(scene.path) && scene.isDirty)
                    Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            }
            Prepare(disableReload);
            yield return new EnterPlayMode(!disableReload);
            yield return UniTask.ToCoroutine(VerifyLifetime);
            yield return new ExitPlayMode();
            Prepare(disableReload);
            yield return new EnterPlayMode(!disableReload);
            yield return UniTask.ToCoroutine(VerifyLifetime);
            yield return new ExitPlayMode();
        }

        private static void Prepare(bool disableReload)
        {
            EditorSettings.enterPlayModeOptionsEnabled = disableReload;
            EditorSettings.enterPlayModeOptions = disableReload ? EnterPlayModeOptions.DisableDomainReload : EnterPlayModeOptions.None;
            EditorSceneManager.OpenScene(Editor.EnvironmentValidation.ScenePath);
        }

        private static async UniTask VerifyLifetime()
        {
            SessionNetwork network = null;
            await Until(() =>
            {
                network = UnityEngine.Object.FindAnyObjectByType<SessionNetwork>();
                return network != null && network.Client != null && network.ObjectResources != null &&
                    network.GetComponent<HeroPlayerController>() != null;
            }, "Unified startup");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../artifacts/hero-input/play-saves", Guid.NewGuid().ToString("N"), "v3"));
            typeof(SessionNetwork).GetProperty(nameof(SessionNetwork.SaveDirectory)).SetValue(network, directory);
            await network.GetComponent<HeroPlayerController>().SetHeroMode(false);
            await network.Connect(true, "127.0.0.1", 27981);
            await Until(() => network.Client.Ready, "Host Ready");
            await Until(() => network.Client.Replica.Current.World.Actors.All(actor => actor.ControllerSlot != 0),
                "Explicit legacy camp mode releases the default hero");
            var world = network.ObjectWorld;
            Assert.That(world, Is.Not.Null);
            Assert.That(world.Index.Count, Is.EqualTo(17));
            Assert.That(network.ActiveSession.StartCount, Is.EqualTo(1));
            Assert.That(world.Camp.Object, Is.SameAs(world.Economy.Object));
            Assert.That(world.Camp.Object.SessionContext, Is.SameAs(world.Context));
            Assert.That(network.ReplicaObjects.Count, Is.Zero, "Host uses the authoritative objects once.");
            await Until(() => network.GetComponent<SessionEntityViews>().Count == world.Index.Count, "Exact Host object views");
            foreach (IEntityBehaviour entity in world.Index.FreezeOrder())
            {
                if (entity.PlacementKey.Length == 0) continue;
                var placement = network.ObjectPlacements.Single(p => p.PlacementKey == entity.PlacementKey);
                Assert.That(entity.Object, Is.SameAs(placement.Loader.ObjectInstance));
            }
            var active = UnityEngine.Object.FindObjectsByType<ObjectInstance>(FindObjectsInactive.Include)
                .SelectMany(i => i.GetAllBehaviors().OfType<IEntityBehaviour>()).Where(e => e.Id != 0).ToArray();
            Assert.That(active.Length, Is.EqualTo(world.Index.Count));
            await network.Client.Send(SessionOperation.SetPaused, value: 1);
            await Until(() => network.Client.Replica.Current.Paused, "Pause");
            double elapsed = world.Elapsed;
            await UniTask.Delay(100, ignoreTimeScale: true);
            Assert.That(world.Elapsed, Is.EqualTo(elapsed));
            int worker = world.Index.Actors[0].Id;
            await network.Client.Send(SessionOperation.PlaceBuilding, actors: new[] { worker }, kind: "house", x: 184);
            await Until(() => world.Index.Buildings.Count == 5, "House creation");
            Assert.That(world.Economy.Stock.Wood, Is.EqualTo(75));
            await Until(() => network.GetComponent<SessionEntityViews>().Count == world.Index.Count, "Dynamic Host view");
            await VerifyRecovery(network);
            ObjectInstance[] owned = world.Index.FreezeOrder().Select(e => e.Object).ToArray();
            var context = world.EntityContext;
            network.Disconnect();
            await UniTask.Yield();
            Assert.That(context.IsAlive, Is.False);
            Assert.That(owned.All(o => o == null || !o.IsActive), Is.True);
            Assert.That(network.ObjectWorld, Is.Null);
        }

        private static async UniTask VerifyRecovery(SessionNetwork network)
        {
            ObjectSession world = network.ObjectWorld;
            var original = world.Index.Find<ActorBehaviour>(13).Object;
            string placement = world.Index.Find<ActorBehaviour>(13).PlacementKey;
            await network.Client.Send(SessionOperation.TrainActors, actors: new[] { 13 }, kind: "archer");
            await Until(() => world.Index.Find<ActorBehaviour>(13).IsTraining, "Training queued");
            await network.Client.Send(SessionOperation.SetPaused, value: 0);
            await Until(() => world.Index.Find<ActorBehaviour>(13).RuleKey == "archer", "Profession completed");
            await network.Client.Send(SessionOperation.SetPaused, value: 1);
            await Until(() => network.Client.Replica.Current.Paused, "Pause promoted world");
            Assert.That(original.IsActive, Is.False);
            Assert.That(world.Index.Find<ActorBehaviour>(13).Object, Is.Not.SameAs(original));
            Assert.That(world.Index.Find<ActorBehaviour>(13).PlacementKey, Is.EqualTo(placement));
            await Until(() => network.GetComponent<SessionEntityViews>().Presentation(13)?.Kind == "archer", "Archer presentation");

            await network.Client.Send(SessionOperation.Save, value: 0);
            string path = Path.Combine(network.SaveDirectory, "slot-00.dnsave.json");
            await Until(() => File.Exists(path) && !network.Server.Storage.Busy, "Save v3");
            string saved = File.ReadAllText(path);
            Assert.That((int)JObject.Parse(saved)["format_version"], Is.EqualTo(Runtime.Save.ObjectWorldSaveJson.FormatVersion));
            int epoch = network.Server.Authority.Epoch;
            var previous = world.EntityContext;
            await network.Client.Send(SessionOperation.Restart);
            await Until(() => network.Client.Ready && network.Client.Replica.Current.Epoch == epoch + 1, "Restart Ready");
            Assert.That(previous.IsAlive, Is.False);
            Assert.That(world.Index.Count, Is.EqualTo(17));
            Assert.That(world.Index.Find<ActorBehaviour>(13).Object, Is.SameAs(original));
            Assert.That(network.Client.Replica.Current.Events.Any(e => e.Type == "banner" && e.Text == "灰松谷 · 第一天"), Is.True);
            Assert.That(network.ReplicaObjects.Count, Is.Zero);

            await network.Client.Send(SessionOperation.BeginLoad, value: 0);
            await Until(() => network.Client.Ready && network.Client.Replica.Current.Epoch == epoch + 2, "Load Ready");
            Assert.That(world.SaveCodec.Serialize(world.CaptureWorld()), Is.EqualTo(saved));
            Assert.That(world.Index.Find<ActorBehaviour>(13).RuleKey, Is.EqualTo("archer"));
            Assert.That(world.Index.Find<ActorBehaviour>(13).Object, Is.Not.SameAs(original));
            Assert.That(world.Index.Buildings.Count, Is.EqualTo(5));
            await Until(() => network.GetComponent<SessionEntityViews>().Count == world.Index.Count, "Restored Host views");

            var unsupported = JObject.Parse(saved);
            unsupported["format_version"] = 1;
            File.WriteAllText(Path.Combine(network.SaveDirectory, "slot-01.dnsave.json"), unsupported.ToString());
            await network.Client.Send(SessionOperation.BeginLoad, value: 1);
            await Until(() => network.Server.Storage.Status == "不支持的存档版本。", "Explicit old version refusal");
            Assert.That(world.SaveCodec.Serialize(world.CaptureWorld()), Is.EqualTo(saved));
            Assert.That(network.Server.Authority.Epoch, Is.EqualTo(epoch + 2));
            Assert.That(network.Client.Ready, Is.True);
        }

        private static async UniTask Until(Func<bool> ready, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 35;
            while (!ready() && Time.realtimeSinceStartupAsDouble < deadline) await UniTask.Yield();
            Assert.That(ready(), Is.True, message);
        }
    }
}
