using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Entry;
using DarkNights.Runtime.Network;
using DarkNights.View;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace DarkNights.Tests
{
    /// <summary>正式Bootstrap的短时装备输入探针；通过真实鼠标／键盘验证采集光标、矿镐、手枪和喷气，不注入角色或地图状态。</summary>
    internal static class EquipmentInputPlayProbe
    {
        private static EditorWindow Game => EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));

        internal static async UniTask StartAndVerify(string output)
        {
            SessionNetwork network = null;
            await Until(() => (network = UnityEngine.Object.FindAnyObjectByType<SessionNetwork>()) != null &&
                network.GetComponent<UIDocument>()?.rootVisualElement.Q("shop") != null, "正式装备UI初始化");
            var ui = network.GetComponent<SessionUiController>();
            var hero = network.GetComponent<HeroPlayerController>();
            ui.ActivateButton("MainMenu", "NewGame");
            await Until(() => network.Client.Ready && hero.Current != null && ui.Page.Length == 0, "地面主角Ready");
            var ship = network.Client.Replica.Current.World.Buildings.Single(b =>
                b.Id == network.Client.Replica.Current.World.Expedition.Ship.Id);
            await Walk(hero, ship.X + 32);
            await PressKey(Key.E);
            var trade = network.GetComponent<ShipTradeHud>();
            await Until(() => trade.ShopOpen, "打开装备商店");
            var root = network.GetComponent<UIDocument>().rootVisualElement;
            foreach (string item in new[] { "pickaxe", "jetpack", "pistol" })
            {
                int revision = hero.Current.InventoryRevision;
                Submit(root.Q<Button>("buy-" + item));
                await Until(() => hero.Current.InventoryRevision > revision, "购买 " + item);
            }
            Submit(root.Q<Button>("close"));
            Directory.CreateDirectory(output);
            await Verify(network, hero, output);
            network.Disconnect();
        }

        private static void Submit(Button button)
        {
            using var evt = NavigationSubmitEvent.GetPooled();
            evt.target = button; button.SendEvent(evt);
        }

        internal static async UniTask Verify(SessionNetwork network, HeroPlayerController hero, string output)
        {
            var ship = network.Client.Replica.Current.World.Buildings.Single(b =>
                b.Id == network.Client.Replica.Current.World.Expedition.Ship.Id);
            await Walk(hero, ship.X + ShipGeometry.RampToe - 64);
            await Until(() => !network.Client.Replica.Current.World.Expedition.Crew.Single(c => c.Id == hero.Current.Id).Boarded,
                "实际步行离开船舱");
            var stage = UnityEngine.Object.FindAnyObjectByType<PinewatchStage>();
            var pointer = typeof(HeroPlayerController).GetField("mining", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hero);
            HeroMiningTarget Target() => (HeroMiningTarget)pointer.GetType().GetProperty("Target").GetValue(pointer);
            await Aim(stage.SceneCamera, hero, 0, -16);
            await Until(() => Target().Present, "地面阶段采集目标与光标恢复");
            var target = Target();
            var cell = new AnyRules.Next.CellCoord(target.U, target.V);
            int durability = network.Terrain.Replica.Query(cell).State.Durability;
            var graphic = UnityEngine.Object.FindAnyObjectByType<TerrainMiningSelectorGraphic>();
            Assert.That(graphic, Is.Not.Null);
            Assert.That((bool)typeof(TerrainMiningSelectorGraphic).GetField("visible", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(graphic), Is.True, "采集框必须真实显示");
            Assert.That(hero.MiningHint, Is.Not.Empty);
            await Until(() => network.GetComponent<UIDocument>().rootVisualElement.Q<Label>("hint").text == hero.MiningHint,
                "当前装备HUD必须显示采集提示");
            Assert.That(hero.MiningHint, Does.Contain("距离"));
            await UniTask.WaitForEndOfFrame(hero);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "mining-cursor.png"));
            await MousePress(true);
            await Until(() => hero.Current.EquipmentAction > 0, "真实左键开始挥镐");
            await UniTask.Delay(400);
            await MousePress(false);
            await Until(() => network.Terrain.Replica.Read(cell).Cell.IsEmpty ||
                network.Terrain.Replica.Query(cell).State.Durability < durability, "矿镐实际修改对应地形耐久");
            Assert.That(network.Client.Replica.Current.World.Expedition.Risk, Is.Zero);

            await PressKey(Key.Digit2);
            await Until(() => hero.Current.SelectedItem == 1, "数字键切换手枪");
            await Aim(stage.SceneCamera, hero, -80, 9);
            await MousePress(true);
            await Until(() => hero.Current.EquipmentCooldown > 0, "真实左键手枪射击");
            await MousePress(false);

            double fuel = hero.Current.JetpackFuel;
            float ground = hero.Current.Height;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Space));
            await Until(() => hero.Current.JetpackFuel < fuel - .1, "长按跳跃衔接喷气消耗燃料");
            Assert.That(hero.Current.Height, Is.GreaterThan(ground + 4));
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            await Until(() => Math.Abs(hero.Current.VerticalSpeed) < .01 && hero.Current.SupportPlatform == 0,
                "松开喷气后落地");
            Assert.That(network.Client.Replica.Current.World.Expedition.Clock, Is.Zero);
        }

        private static async UniTask Aim(Camera camera, HeroPlayerController hero, float dx, float dh)
        {
            Vector3 point = camera.WorldToScreenPoint(new Vector3((hero.Current.X + dx) / 100f,
                (hero.Current.Height + dh) / 100f, 0));
            Game.Focus();
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(point.x, point.y) });
            await UniTask.Delay(150);
        }

        private static async UniTask MousePress(bool pressed)
        {
            var state = new MouseState { position = Mouse.current.position.ReadValue() };
            if (pressed) state = state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left);
            InputSystem.QueueStateEvent(Mouse.current, state);
            await UniTask.Delay(100);
        }

        private static async UniTask PressKey(UnityEngine.InputSystem.Key key)
        {
            Game.Focus();
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            await UniTask.Delay(100);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            await UniTask.Delay(150);
        }

        private static async UniTask Walk(HeroPlayerController hero, float x)
        {
            float deadline = Time.realtimeSinceStartup + 12;
            while (Math.Abs(hero.Current.X - x) > 4)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "步行离开坡道到地面");
                await PressKey(hero.Current.X < x ? UnityEngine.InputSystem.Key.D : UnityEngine.InputSystem.Key.A);
            }
        }

        private static async UniTask Until(Func<bool> predicate, string step)
        {
            float deadline = Time.realtimeSinceStartup + 12;
            while (!predicate())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), step);
                Game.Repaint(); EditorApplication.QueuePlayerLoopUpdate();
                await UniTask.Yield();
            }
        }
    }
}
