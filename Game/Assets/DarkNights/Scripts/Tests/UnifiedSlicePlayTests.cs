using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Entry;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.View;
using GameCore.Objects.Runner;
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
            SessionState.SetBool("DarkNights.UnifiedSlice", false);
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
            SessionState.SetBool("DarkNights.UnifiedSlice", true);
            EditorSceneManager.OpenScene(Editor.EnvironmentValidation.ScenePath);
        }

        private static async UniTask VerifyLifetime()
        {
            SessionNetwork network = null;
            await Until(() =>
            {
                network = UnityEngine.Object.FindAnyObjectByType<SessionNetwork>();
                return network != null && network.Client != null && network.ObjectResources != null;
            }, "Unified startup");
            await network.Connect(true, "127.0.0.1", 27981);
            await Until(() => network.Client.Ready, "Host Ready");
            var world = network.ObjectWorld;
            Assert.That(world, Is.Not.Null);
            Assert.That(network.ActiveSession.StartCount, Is.EqualTo(1));
            Assert.That(world.Camp.Object, Is.SameAs(world.Economy.Object));
            Assert.That(world.Camp.Object.SessionContext, Is.SameAs(world.Context));
            Assert.That(network.ReplicaObjects.Count, Is.Zero, "Host uses the authoritative objects once.");
            await Until(() => network.GetComponent<SessionEntityViews>().Count == world.Index.Count, "Exact Host object views");
            foreach (IEntityBehaviour entity in world.Index.FreezeOrder())
            {
                var placement = network.ObjectPlacements.Single(p => p.PlacementKey == entity.PlacementKey);
                Assert.That(entity.Object, Is.SameAs(placement.Loader.ObjectInstance));
            }
            var active = UnityEngine.Object.FindObjectsByType<ObjectInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .SelectMany(i => i.GetAllBehaviors().OfType<IEntityBehaviour>()).Where(e => e.Id != 0).ToArray();
            Assert.That(active.Length, Is.EqualTo(world.Index.Count));
            await network.Client.Send(SessionOperation.SetPaused, value: 1);
            await Until(() => network.Client.Replica.Current.Paused, "Pause");
            double elapsed = world.Elapsed;
            await UniTask.Delay(100, ignoreTimeScale: true);
            Assert.That(world.Elapsed, Is.EqualTo(elapsed));
            int worker = world.Index.Actors[0].Id;
            await network.Client.Send(SessionOperation.PlaceBuilding, actors: new[] { worker }, kind: "house", x: 184);
            await Until(() => world.Index.Buildings.Count == 3, "House creation");
            Assert.That(world.Economy.Stock.Wood, Is.EqualTo(75));
            await Until(() => network.GetComponent<SessionEntityViews>().Count == world.Index.Count, "Dynamic Host view");
            ObjectInstance[] owned = world.Index.FreezeOrder().Select(e => e.Object).ToArray();
            var context = world.EntityContext;
            network.Disconnect();
            await UniTask.Yield();
            Assert.That(context.IsAlive, Is.False);
            Assert.That(owned.All(o => o == null || !o.IsActive), Is.True);
            Assert.That(network.ObjectWorld, Is.Null);
        }

        private static async UniTask Until(Func<bool> ready, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 35;
            while (!ready() && Time.realtimeSinceStartupAsDouble < deadline) await UniTask.Yield();
            Assert.That(ready(), Is.True, message);
        }
    }
}
