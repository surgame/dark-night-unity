using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Entry;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using DarkNights.View;
using GameCore.Debugging;
using GameCore.Interactions;
using GameCore.Objects.Definition;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DarkNights.Tests
{
    /// <summary>真实 Bootstrap 主菜单及快速局 Play 验证；走同一入口和真实鼠标采矿，覆盖菜单显隐、重复进入、干净地图及正常开局。</summary>
    public sealed class QuickTestHubPlayTests
    {
        private Mouse mouse;
        private Mouse[] previousMice;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private bool inputChanged;
        private static EditorWindow Game => EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        private static string Output => Path.GetFullPath("../artifacts/quick-tests-20261002/play");

        [UnityTest]
        public IEnumerator MainMenuQuickLaunchMinesAndRepeatedEntryStartsFresh()
        {
            EditorSceneManager.OpenScene(GameScenePaths.Bootstrap); Game.Focus();
            yield return new EnterPlayMode();
            yield return UniTask.ToCoroutine(Verify);
            yield return new ExitPlayMode();
        }

        private async UniTask Verify()
        {
            using var viewport = new TerrainGameViewTestSize(Game, 1280, 800);
            Application.runInBackground = true;
            await Until(() => Object.FindAnyObjectByType<QuickTestHub>()?.Available == true, 90);
            var hub = Object.FindAnyObjectByType<QuickTestHub>();
            var ui = hub.GetComponent<SessionUiController>();
            var network = hub.GetComponent<SessionNetwork>();
            var storage = typeof(SessionNetwork).GetField("storage", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(network);
            storage.GetType().GetMethod("Initialize").Invoke(storage, new object[]
                { new[] { "--dn-save-dir", Path.Combine(Output, "saves-" + Guid.NewGuid().ToString("N")) } });
            string normalSaves = network.SaveDirectory;
            Assert.That(hub.PanelRegistered, Is.True);
            ui.ActivateButton("MainMenu", "Help"); await UniTask.Yield();
            Assert.That(hub.Available, Is.False); Assert.That(hub.PanelRegistered, Is.False);
            ui.ActivateButton("Help", "Back"); await UniTask.Yield();
            Assert.That(hub.PanelRegistered, Is.True);
            await VerifyAbort(hub, network, normalSaves);
            PrepareMouse();
            Assert.That(hub.RequestLaunch(), Is.True); Assert.That(hub.RequestLaunch(), Is.False);
            await Until(() => network.Client.Ready && ui.Page == "", 150);
            Assert.That(hub.PanelRegistered, Is.False);
            Assert.That(YYInteractionSessionService.Instance.IsBlocked(YYInteractionBlockFlags.GameplayActions), Is.False);
            Assert.That(network.SaveDirectory, Does.Contain(Path.Combine("QuickTests", QuickTestPreset.LandedPickaxeId)));
            var hero = hub.GetComponent<HeroPlayerController>();
            await Until(() => hero.Current != null, 15);
            var expedition = network.Client.Replica.Current.World.Expedition;
            Assert.That(expedition.Journey.Phase, Is.EqualTo(JourneyPhase.Landed));
            Assert.That(expedition.Crew.Single(crew => crew.Id == hero.Current.Id).Boarded, Is.False);
            var rules = network.ObjectWorld.Resources.Equipment.Mining(hero.Current.Slot0Definition);
            Assert.That(rules, Is.Not.Null);
            string firstWorld = network.Terrain.Replica.World.WorldId.ToString();
            Assert.That(TerrainMiningQuery.FirstSurface(network.Terrain.Replica, hero.Current.X,
                hero.Current.Height + rules.HandHeight, 0, -1, rules.Reach, out var target, out _), Is.True);
            uint tile = network.Terrain.Replica.Read(target).Cell.TileId;
            var stage = Object.FindAnyObjectByType<PinewatchStage>();
            Vector2 point = default;
            float aiming = Time.realtimeSinceStartup + 15;
            while (!hero.MiningHint.Contains("左键／按住采集"))
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(aiming), hero.MiningHint);
                point = stage.SceneCamera.WorldToScreenPoint(new Vector3(hero.Current.X / 100,
                    TerrainMiningGeometry.CenterHeight(target.V) / 100, 0));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                Game.Repaint(); await UniTask.Yield();
            }
            Directory.CreateDirectory(Output);
            ScreenCapture.CaptureScreenshot(Path.Combine(Output, "landed-pickaxe.png")); await UniTask.Yield();
            float deadline = Time.realtimeSinceStartup + 20;
            while (!network.Terrain.Replica.Read(target).Cell.IsEmpty)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), hero.MiningHint);
                point = stage.SceneCamera.WorldToScreenPoint(new Vector3(hero.Current.X / 100,
                    TerrainMiningGeometry.CenterHeight(target.V) / 100, 0));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
                Game.Repaint(); EditorApplication.QueuePlayerLoopUpdate(); await UniTask.Yield();
            }
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); await UniTask.Yield();
            await Intent(ui, "Save");
            await Until(() => File.Exists(Path.Combine(network.SaveDirectory, "slot-00.dnsave.json")) &&
                !network.Server.Storage.Busy, 30);
            Assert.That(File.Exists(Path.Combine(normalSaves, "slot-00.dnsave.json")), Is.False);
            int epoch = network.Client.Replica.Current.Epoch;
            await network.Client.Send(SessionOperation.Restart);
            await Until(() => network.Client.Ready && network.Client.Replica.Current.Epoch > epoch, 60);
            Assert.That(network.Terrain.Replica.Read(target).Cell.TileId, Is.EqualTo(tile));
            Assert.That(network.Client.Replica.Current.World.Expedition.Journey.Phase, Is.EqualTo(JourneyPhase.Landed));
            await Intent(ui, "Menu"); await UniTask.Yield();
            Assert.That(hub.PanelRegistered, Is.False);
            ui.ActivateButton("PauseMenu", "MainMenu");
            await Until(() => hub.Available && hub.PanelRegistered, 30);
            Assert.That(network.SaveDirectory, Is.EqualTo(normalSaves));
            Assert.That(hub.RequestLaunch(), Is.True);
            await Until(() => network.Client.Ready && ui.Page == "", 150);
            Assert.That(network.Terrain.Replica.World.WorldId.ToString(), Is.Not.EqualTo(firstWorld));
            Assert.That(network.Terrain.Replica.Read(target).Cell.TileId, Is.EqualTo(tile));
            Assert.That(network.Client.Replica.Current.World.Expedition.Journey.Phase, Is.EqualTo(JourneyPhase.Landed));
            await Intent(ui, "Menu"); await UniTask.Yield();
            ui.ActivateButton("PauseMenu", "MainMenu");
            await Until(() => hub.Available, 30);
            ui.ActivateButton("MainMenu", "NewGame");
            await Until(() => network.Client.Ready && ui.Page == "", 90);
            Assert.That(network.Client.Replica.Current.World.Expedition.Journey.Phase, Is.EqualTo(JourneyPhase.Orbit));
            Assert.That(network.SaveDirectory, Is.EqualTo(normalSaves));
            await Intent(ui, "Save");
            await Until(() => File.Exists(Path.Combine(normalSaves, "slot-00.dnsave.json")) && !network.Server.Storage.Busy, 30);
            await Intent(ui, "Menu"); await UniTask.Yield();
            ui.ActivateButton("PauseMenu", "MainMenu");
            await Until(() => hub.Available, 30);
            ui.ActivateButton("MainMenu", "Continue");
            await Until(() => network.Client.Ready && network.Client.Replica.Current.Epoch > 1 && !network.Server.Storage.Busy, 90);
            Assert.That(network.Client.Replica.Current.World.Expedition.Journey.Phase, Is.EqualTo(JourneyPhase.Orbit));
            Assert.That(network.SaveDirectory, Is.EqualTo(normalSaves));
            network.Disconnect(); RestoreMouse();

        }

        private static async UniTask VerifyAbort(QuickTestHub hub, SessionNetwork network, string normalSaves)
        {
            var flow = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch")
                .SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            var preset = QuickTestPreset.Create(QuickTestPreset.LandedPickaxeId, flow);
            var generation = network.Terrain.SelectNew(preset);
            Assert.That(network.Terrain.Selecting, Is.True);
            Assert.That(hub.RequestLaunch(), Is.False);
            network.Disconnect();
            try { await generation; Assert.Fail("取消的生成不得重新发布候选。"); }
            catch (OperationCanceledException) { }
            Assert.That(typeof(SessionTerrainNetwork).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(network.Terrain), Is.Null);
            Assert.That(network.SaveDirectory, Is.EqualTo(normalSaves));
            await Until(() => hub.Available && hub.PanelRegistered, 30);
            Assert.That(network.Terrain.SelectionStatus, Does.Not.Contain("正在生成"));
            bool enabled = flow.Enabled;
            try
            {
                flow.Enabled = false;
                var error = new System.Text.RegularExpressions.Regex("快速着陆需要启用正式星球航程配置");
                LogAssert.Expect(LogType.Exception, error); LogAssert.Expect(LogType.Exception, error);
                Assert.That(hub.RequestLaunch(), Is.True);
                await Until(() => network.Status.Contains("快速着陆需要"), 15);
            }
            finally { flow.Enabled = enabled; }
            await Until(() => hub.Available && hub.PanelRegistered, 30);
            Assert.That(network.SaveDirectory, Is.EqualTo(normalSaves));
            Assert.That(network.Client.Replica.Current, Is.Null);
        }

        private void PrepareMouse()
        {
            previousMice = InputSystem.devices.OfType<Mouse>().Where(device => device.enabled).ToArray();
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode; inputChanged = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse = InputSystem.AddDevice<Mouse>("Quick test verification");
            InputUser.PerformPairingWithDevice(mouse, Object.FindAnyObjectByType<PinewatchStage>().InputPlayer.user);
            foreach (var original in previousMice) InputSystem.DisableDevice(original);
        }
        private void RestoreMouse()
        {
            if (!inputChanged) return;
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            foreach (var original in previousMice) if (original.added) InputSystem.EnableDevice(original);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput; inputChanged = false;
        }
        private static UniTask Intent(SessionUiController ui, string action) =>
            (UniTask)typeof(SessionUiController).GetMethod("Execute", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ui, new object[] { new InputIntent(action, Array.Empty<int>()) });
        private static async UniTask Until(Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "快速测试等待超时。");
                Game.Repaint(); EditorApplication.QueuePlayerLoopUpdate(); await UniTask.Yield();
            }
            await UniTask.Yield();
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying)
            {
                RestoreMouse(); Object.FindAnyObjectByType<SessionNetwork>()?.Disconnect();
                yield return new ExitPlayMode();
            }
        }
    }
}
