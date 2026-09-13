using System;
using System.Collections;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Runner.DI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 在真实 Unity PlayerLoop 和生成的调度器中检查准备、激活、退休及再次装配。
    /// 分别重复进入开启和关闭域重载的 Play；测试只使用临时空场景并恢复原制作场景与选项。
    /// </summary>
    public sealed class UnifiedObjectPlayTests
    {
        private const string SettingsKey = "DarkNights.UnifiedObjectPlay.Settings";
        private const string SceneKey = "DarkNights.UnifiedObjectPlay.Scene";

        [UnityTest]
        public IEnumerator RepeatedPlayWithDomainReload() => RepeatPlay(false);

        [UnityTest]
        public IEnumerator RepeatedPlayWithoutDomainReload() => RepeatPlay(true);

        [UnityTearDown]
        public IEnumerator RestoreEditor()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            int saved = SessionState.GetInt(SettingsKey, -1);
            if (saved < 0) yield break;
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)(saved >> 1);
            EditorSettings.enterPlayModeOptionsEnabled = (saved & 1) != 0;
            string path = SessionState.GetString(SceneKey, "");
            if (!string.IsNullOrEmpty(path)) EditorSceneManager.OpenScene(path);
            SessionState.EraseInt(SettingsKey);
            SessionState.EraseString(SceneKey);
        }

        private static IEnumerator RepeatPlay(bool disableReload)
        {
            if (SessionState.GetInt(SettingsKey, -1) < 0)
            {
                SessionState.SetInt(SettingsKey, ((int)EditorSettings.enterPlayModeOptions << 1) |
                    (EditorSettings.enterPlayModeOptionsEnabled ? 1 : 0));
                SessionState.SetString(SceneKey, EditorSceneManager.GetActiveScene().path);
                if (!string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path) && EditorSceneManager.GetActiveScene().isDirty)
                    Assert.That(EditorSceneManager.SaveOpenScenes(), Is.True);
            }
            // 域重载恢复协程的执行位置，但不会保存局部循环计数；两个进入点必须有独立执行位置。
            PreparePlay(disableReload);
            yield return new EnterPlayMode(!disableReload);
            yield return VerifyOneLifetime();
            yield return new ExitPlayMode();
            PreparePlay(disableReload);
            yield return new EnterPlayMode(!disableReload);
            yield return VerifyOneLifetime();
            yield return new ExitPlayMode();
        }

        private static void PreparePlay(bool disableReload)
        {
            EditorSettings.enterPlayModeOptionsEnabled = disableReload;
            EditorSettings.enterPlayModeOptions = disableReload ? EnterPlayModeOptions.DisableDomainReload : EnterPlayModeOptions.None;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static IEnumerator VerifyOneLifetime()
        {
            GameObject manager = null;
            if (BehaviourUpdateManager.Instance == null)
            {
                manager = new GameObject("UnifiedObjectTestLoop");
                manager.AddComponent<BehaviourUpdateManager>();
            }
            var container = new DIContainer();
            container.Initialize();
            container.Register(new UnifiedObjectProbeDependency(1));
            using var session = ObjectSessionContext.CreateAuthority(container, () => true);
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.EditorSetIdentity(Guid.NewGuid().ToString("N"), "test.play-lifetime", true);
            definition.UseMonolithicPooling = true;
            definition.BehaviourTypes.Add(typeof(UnifiedObjectProbeBehaviour).AssemblyQualifiedName);
            definition.SharedConfigs.Add(new UnifiedObjectProbeConfig());
            var instance = new GameObject("UnifiedObjectPlayTest").AddComponent<ObjectInstance>();
            try
            {
                instance.Initialize("one", definition, session: session, activate: false);
                var behaviour = instance.GetBehaviour<UnifiedObjectProbeBehaviour>();
                yield return null;
                Assert.That(behaviour.UpdateCount, Is.Zero);
                session.Activate();
                instance.Activate();
                instance.Activate();
                yield return null;
                yield return null;
                Assert.That(behaviour.StartCount, Is.EqualTo(1));
                Assert.That(behaviour.UpdateCount, Is.GreaterThan(0), "Generated dispatcher must run in the real PlayerLoop.");
                instance.Retire();
                int retired = behaviour.UpdateCount;
                yield return null;
                yield return null;
                Assert.That(behaviour.UpdateCount, Is.EqualTo(retired));
                instance.Initialize("two", definition, session: session, activate: false);
                Assert.That(instance.GetBehaviour<UnifiedObjectProbeBehaviour>(), Is.SameAs(behaviour));
                instance.Activate();
                yield return null;
                yield return null;
                Assert.That(behaviour.StartCount, Is.EqualTo(1));
                Assert.That(behaviour.UpdateCount, Is.GreaterThan(0));
                session.Dispose();
                retired = behaviour.UpdateCount;
                yield return null;
                Assert.That(behaviour.UpdateCount, Is.EqualTo(retired));
                Assert.That(behaviour.State, Is.Null);
            }
            finally
            {
                instance.Release();
                UnityEngine.Object.Destroy(instance.gameObject);
                UnityEngine.Object.Destroy(definition);
                if (manager != null) UnityEngine.Object.Destroy(manager);
                session.Dispose();
                container.OnReturnToPool();
            }
        }
    }
}
