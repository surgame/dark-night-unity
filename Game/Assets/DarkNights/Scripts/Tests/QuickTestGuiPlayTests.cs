using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.Entry;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using GameCore.Debugging;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 有界的人工 GUI 验收：注入 F1 打开真实 Hub 后等待实际鼠标点击启动按钮。
    /// 默认测试不执行此项；显式运行后两分钟内必须点击，否则失败并退出 Play，不留下会话或虚拟设备。
    /// </summary>
    public sealed class QuickTestGuiPlayTests
    {
        private Keyboard keyboard;
        private static EditorWindow Game => EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));

        [UnityTest, Explicit("实际 GUI 点击验收，需要在两分钟内点击启动所选测试。")]
        public IEnumerator GuiButtonStartsLandedSession()
        {
            EditorSceneManager.OpenScene(GameScenePaths.Bootstrap); Game.Focus();
            yield return new EnterPlayMode();
            yield return UniTask.ToCoroutine(Verify);
            yield return new ExitPlayMode();
        }

        private async UniTask Verify()
        {
            await Until(() => UnityEngine.Object.FindAnyObjectByType<QuickTestHub>()?.Available == true, 90);
            Assert.That(QuickTestSelection.SelectedId, Is.EqualTo(QuickTestPreset.LandedPickaxeId));
            Assert.That(EditorPrefs.GetString(QuickTestSelection.PreferenceKey), Is.EqualTo(QuickTestPreset.LandedPickaxeId));
            keyboard = InputSystem.AddDevice<Keyboard>("Quick GUI F1 verification");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
            await UniTask.Delay(100);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await UniTask.Delay(200);
            var debug = UnityEngine.Object.FindAnyObjectByType<RuntimeDebugHub>();
            Assert.That((bool)typeof(RuntimeDebugHub).GetField("_showWindow", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(debug), Is.True, "F1 必须打开真实 Hub。");
            InputSystem.RemoveDevice(keyboard); keyboard = null;
            var network = UnityEngine.Object.FindAnyObjectByType<SessionNetwork>();
            await Until(() => network.Client.Ready, 120);
            Assert.That(network.Client.Replica.Current.World.Expedition.Journey.Phase, Is.EqualTo(JourneyPhase.Landed));
            var hero = network.GetComponent<HeroPlayerController>();
            Assert.That(hero.Current, Is.Not.Null);
            Assert.That(network.ObjectResources.Equipment.Mining(hero.Current.Slot0Definition), Is.Not.Null);
            Assert.That(network.SaveDirectory, Does.Contain(Path.Combine("QuickTests", QuickTestPreset.LandedPickaxeId)));
            Assert.That(network.GetComponent<QuickTestHub>().PanelRegistered, Is.False);
            await UniTask.Delay(500);
            string output = Path.GetFullPath("../artifacts/quick-tests-20261002/play/gui-landed-pickaxe.png");
            ScreenCapture.CaptureScreenshot(output); await UniTask.Delay(300);
            network.Disconnect();
        }

        private static async UniTask Until(Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "GUI 验收等待超时。");
                Game.Repaint(); EditorApplication.QueuePlayerLoopUpdate(); await UniTask.Yield();
            }
            await UniTask.Yield();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (Application.isPlaying)
            {
                UnityEngine.Object.FindAnyObjectByType<SessionNetwork>()?.Disconnect();
                yield return new ExitPlayMode();
            }
        }
    }
}
