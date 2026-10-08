using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Entry;
using DarkNights.Runtime.Network;
using DarkNights.View;
using GameCore.Interactions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkNights.Tests
{
    /// <summary>正式 Bootstrap 的 UI 回归；真实输入经 YYGC 到 Host，验证可见弹窗、购买及退出后恢复操作。</summary>
    public sealed class ShipTradePlayTests
    {
        private const string SceneKey = "DarkNights.ShipTradePlay.Scene";
        private static EditorWindow Game => EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        private static string Output => Path.GetFullPath("../artifacts/equipment-restore-20261007/presentation");

        [Test]
        public void RestoredBuildSettingsAreNotRewrittenWhileLocked()
        {
            Directory.CreateDirectory(Output);
            string path = Path.Combine(Output, "unchanged-settings.fixture");
            byte[] bytes = { 1, 2, 3 };
            File.WriteAllBytes(path, bytes);
            using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var restore = typeof(Editor.GamePlayerBuild).GetMethod("RestoreSettings",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(restore, Is.Not.Null);
            Assert.DoesNotThrow(() => restore.Invoke(null, new object[] { path, bytes }));
        }

        [UnityTest]
        public IEnumerator ConsoleChineseLogsStayBounded()
        {
            SessionState.SetString(SceneKey, EditorSceneManager.GetActiveScene().path);
            EditorSceneManager.OpenScene(Editor.EnvironmentValidation.ScenePath);
            Game.Focus();
            yield return new EnterPlayMode();
            yield return UniTask.ToCoroutine(VerifyConsole);
            yield return new ExitPlayMode();
        }

        private static async UniTask VerifyConsole()
        {
            SmartConsoleLogBridge bridge = null;
            await Until(() => (bridge = UnityEngine.Object.FindAnyObjectByType<SmartConsoleLogBridge>()) != null,
                "有界控制台日志接入");
            bool initiallyBlocked = YYInteractionSessionService.Instance.IsBlocked(YYInteractionBlockFlags.GameplayActions);
            Debug.Log("控制台中文验证：地图地形、主角移动、跳跃、镜头、联机与保存恢复。");
            for (int i = 0; i < 3; i++)
            {
                await KeyPress(Key.F10);
                await UniTask.Delay(1000);
                Assert.That(bridge.Rendered, Is.LessThanOrEqualTo(SmartConsoleLogBridge.MaximumRendered));
                Assert.That(bridge.Queued, Is.LessThanOrEqualTo(SmartConsoleLogBridge.MaximumQueued));
                var text = bridge.GetComponentsInChildren<TMPro.TMP_Text>(true).FirstOrDefault(t => t.text.Contains("控制台中文验证"));
                Assert.That(text, Is.Not.Null, "中文日志必须进入可见面板");
                Assert.That(text.font.HasCharacter('中', true, true), Is.True, "中文日志必须有回退字形");
                await KeyPress(Key.F10);
            }
            Assert.That(bridge.SuppressedFontWarnings, Is.Zero, "字体修复后不应再产生面板缺字警告");
            Assert.That(bridge.Dropped, Is.Zero, "短时验证不应出现日志风暴");
            Assert.That(YYInteractionSessionService.Instance.IsBlocked(YYInteractionBlockFlags.GameplayActions),
                Is.EqualTo(initiallyBlocked), "关闭控制台必须保留原菜单的模态状态");
        }

        [UnityTest]
        public IEnumerator EquipmentRendersAndUsesRealInput()
        {
            SessionState.SetString(SceneKey, EditorSceneManager.GetActiveScene().path);
            EditorSceneManager.OpenScene(Editor.EnvironmentValidation.ScenePath);
            Game.Focus();
            yield return new EnterPlayMode();
            yield return UniTask.ToCoroutine(() => EquipmentInputPlayProbe.StartAndVerify(Output));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ShopRendersBuysAndReleasesInput()
        {
            SessionState.SetString(SceneKey, EditorSceneManager.GetActiveScene().path);
            EditorSceneManager.OpenScene(Editor.EnvironmentValidation.ScenePath);
            Game.Focus();
            yield return new EnterPlayMode();
            yield return UniTask.ToCoroutine(Verify);
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if (Application.isPlaying)
            {
                if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current,
                    new MouseState { position = Mouse.current.position.ReadValue() });
                yield return new ExitPlayMode();
            }
            string previous = SessionState.GetString(SceneKey, "");
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            SessionState.EraseString(SceneKey);
        }

        private static async UniTask Verify()
        {
            SessionNetwork network = null;
            await Until(() =>
            {
                network = UnityEngine.Object.FindAnyObjectByType<SessionNetwork>();
                return network != null && network.GetComponent<UIDocument>()?.rootVisualElement.Q("shop") != null;
            }, "正式 UI 初始化");
            Game.Focus();
            Directory.CreateDirectory(Output);
            var ui = network.GetComponent<SessionUiController>();
            var hero = network.GetComponent<HeroPlayerController>();
            var trade = network.GetComponent<ShipTradeHud>();
            var document = network.GetComponent<UIDocument>();
            Assert.That(document.panelSettings.themeStyleSheet, Is.Not.Null);
            ui.ActivateButton("MainMenu", "NewGame");
            await Until(() => network.Client.Ready && hero.Current != null && ui.Page.Length == 0, "Host Ready");
            Assert.That(network.Client.Replica.Current.World.Expedition.Journey.Phase,
                Is.EqualTo(DarkNights.Core.ViewData.JourneyPhase.Landed));
            Assert.That(network.Client.Replica.Current.World.Expedition.Crew.Single(c => c.Id == hero.Current.Id).Boarded, Is.False);
            var root = document.rootVisualElement;
            await Until(() => root.Q<Label>("credits").worldBound.width > 100, "装备 HUD 完成布局");
            Assert.That(root.worldBound.height, Is.GreaterThan(100));
            Assert.That(root.Q<Label>("credits").text, Does.Contain("30"));
            var font = root.Q<Label>("credits").resolvedStyle.unityFontDefinition.fontAsset;
            Assert.That(font, Is.Not.Null, "UI Toolkit 必须使用 TextCore 字体而非 UGUI 的动态 Font 图集");
            Assert.That(font.HasCharacter('矿', true, true), Is.True, "中文商品必须有可渲染字形");
            Assert.That(hero.Current.Slot0, Is.EqualTo(4));
            Assert.That(hero.Current.JetpackOwned, Is.False);
            var picked = root.panel.Pick(new Vector2(root.worldBound.center.x, root.worldBound.height * .2f));
            Assert.That(picked == null || !root.Contains(picked), Is.True, "透明全屏容器不能挡住游戏指针");

            float target = float.NaN;
            // 船体横坐标由冻结 Building 投影读取，终端相对位置来自本批原生 Prefab。
            int shipId = network.Client.Replica.Current.World.Expedition.Ship.Id;
            foreach (var building in network.Client.Replica.Current.World.Buildings)
                if (building.Id == shipId) target = building.X + 32;
            await Walk(hero, target);
            await KeyPress(Key.E);
            await Until(() => trade.ShopOpen, "E 打开商店");
            Assert.That(YYInteractionSessionService.Instance.IsBlocked(YYInteractionBlockFlags.GameplayActions), Is.True);
            var shop = root.Q("shop");
            Assert.That(shop.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(shop.worldBound.height, Is.GreaterThan(100));
            float stopped = hero.Current.X;
            await KeyPress(Key.A, .25f);
            Assert.That(hero.Current.X, Is.EqualTo(stopped).Within(1), "商店打开时不移动");
            Submit(root.Q<Button>("buy-pickaxe"));
            await Until(() => hero.Current.Slot1 == 2, "购买矿镐反馈");
            Assert.That(network.Client.Replica.Current.World.Camp.Credits, Is.EqualTo(26));
            Assert.That(root.Q<Button>("buy-pickaxe").enabledSelf, Is.False);
            Submit(root.Q<Button>("buy-jetpack"));
            await Until(() => hero.Current.JetpackOwned, "购买喷气背包反馈");
            Assert.That(network.Client.Replica.Current.World.Camp.Credits, Is.EqualTo(12));
            Assert.That(root.Q<Label>("jetpack").text, Is.EqualTo("喷气背包"));
            Assert.That(hero.Current.JetpackEquipped, Is.True);
            Assert.That(hero.Current.JetpackFuel, Is.GreaterThan(0));
            Submit(root.Q<Button>("buy-pistol"));
            await Until(() => hero.Current.Slot1 == 1, "购买手枪反馈");
            Assert.That(network.Client.Replica.Current.World.Camp.Credits, Is.EqualTo(2));

            var hud = (DarkNights.View.CampHudBehaviour)typeof(SessionUiController).GetField("hud",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ui);
            Assert.That(hud, Is.Not.Null);
            LogAssert.Expect(LogType.Log, "[Gameplay] <color=#F2B66D>界面提示: SESSION_FEEDBACK_WARNING_TEST</color>");
            hud.ShowMessage("SESSION_FEEDBACK_WARNING_TEST");
            LogAssert.Expect(LogType.Log, "[Gameplay] <color=#83CBEA>会话横幅: SESSION_BANNER_TEST · detail</color>");
            hud.PresentEvent(new DarkNights.Core.ViewData.PresentationEvent(1, 0, "banner", "SESSION_BANNER_TEST", "detail"), 0);
            await UniTask.Yield();
            var toast = typeof(DarkNights.View.CampHudBehaviour).GetField("toastPanel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var banner = typeof(DarkNights.View.CampHudBehaviour).GetField("banner",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(((RectTransform)toast.GetValue(hud)).gameObject.activeSelf, Is.False);
            Assert.That(((RectTransform)banner.GetValue(hud)).gameObject.activeSelf, Is.False);

            foreach (var size in new[] { new Vector2Int(1280, 800), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) })
            {
                using var view = new TerrainGameViewTestSize(Game, size.x, size.y);
                await UniTask.Delay(250);
                var close = root.Q<Button>("close");
                Assert.That(root.worldBound.Contains(close.worldBound.center), Is.True, "关闭按钮必须位于屏幕内");
                Assert.That(close.worldBound.width, Is.GreaterThan(20));
                await UniTask.WaitForEndOfFrame(trade);
                ScreenCapture.CaptureScreenshot(Path.Combine(Output, $"shop-{size.x}x{size.y}.png"));
                await UniTask.Yield();
            }

            Submit(root.Q<Button>("close"));
            Assert.That(trade.ShopOpen, Is.False);
            AssertUnlocked();
            await KeyPress(Key.D, .18f);
            Assert.That(hero.Current.X, Is.GreaterThan(stopped + 2), "关闭后角色恢复移动");
            await KeyPress(Key.F10);
            Assert.That(YYInteractionSessionService.Instance.IsBlocked(YYInteractionBlockFlags.GameplayActions), Is.True);
            await KeyPress(Key.F10);
            AssertUnlocked();
            await Walk(hero, target);
            await KeyPress(Key.E);
            await Until(() => trade.ShopOpen, "重复打开商店");
            await KeyPress(Key.Escape);
            Assert.That(trade.ShopOpen, Is.False);
            Assert.That(ui.Page, Is.Empty, "Esc 只关商店，不顺带打开暂停页");
            AssertUnlocked();
            await KeyPress(Key.E);
            await Until(() => trade.ShopOpen, "禁用组件前打开商店");
            trade.enabled = false;
            Assert.That(trade.ShopOpen, Is.False);
            AssertUnlocked();
            trade.enabled = true;
            await KeyPress(Key.E);
            await Until(() => trade.ShopOpen, "断线前打开商店");
            network.Disconnect();
            await Until(() => !trade.ShopOpen && ui.Page == "MainMenu", "断线关闭商店并返回菜单");
            Assert.That(YYInteractionSessionService.Instance.ActiveSessions,
                Has.None.Matches<YYInteractionSession>(session => session.Kind == "dark_nights.ship_shop"));
        }

        private static void AssertUnlocked() =>
            Assert.That(YYInteractionSessionService.Instance.IsBlocked(YYInteractionBlockFlags.GameplayActions), Is.False);

        private static void Submit(Button button)
        {
            Assert.That(button.enabledInHierarchy, Is.True);
            using var evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }

        private static async UniTask KeyPress(Key key, float seconds = .1f)
        {
            Game.Focus();
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            await UniTask.Delay(TimeSpan.FromSeconds(seconds));
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            await UniTask.Delay(150);
        }

        private static async UniTask Walk(HeroPlayerController hero, float x)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (Math.Abs(hero.Current.X - x) > 4)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "真实船内行走到商店");
                await KeyPress(hero.Current.X < x ? Key.D : Key.A, .1f);
            }
        }

        private static async UniTask Until(Func<bool> predicate, string phase)
        {
            float deadline = Time.realtimeSinceStartup + 45;
            while (!predicate())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), phase);
                Game.Repaint(); EditorApplication.QueuePlayerLoopUpdate();
                await UniTask.Yield();
            }
        }
    }
}
