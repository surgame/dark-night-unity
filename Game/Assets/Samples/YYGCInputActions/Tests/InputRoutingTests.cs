using System;
using GameCore.Interactions;
using GameCore.PlayerInputs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GameCore.Samples.InputActions.Tests
{
    /// <summary>
    /// 使用原生键盘状态事件验证短按、跨模式按钮隔离和通道取消；不替换 InputAction 状态机。
    /// 使用官方测试运行时隔离设备和焦点，退出恢复原输入系统；测量仅覆盖缓存路由的稳定循环。
    /// </summary>
    public sealed class InputRoutingTests : InputTestFixture
    {
        private Keyboard keyboard;
        private InputActionAsset asset;
        private YYInteractionSessionService sessions;
        private YYInputActionService input;
        private InputAction first, second;
        [SetUp]
        public override void Setup()
        {
            base.Setup();
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            keyboard = InputSystem.AddDevice<Keyboard>();
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            first = asset.AddActionMap("Player").AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            second = asset.AddActionMap("Camp").AddAction("Confirm", InputActionType.Button, "<Keyboard>/space");
            asset.devices = new InputDevice[] { keyboard };
            sessions = new YYInteractionSessionService(); input = new YYInputActionService(sessions);
            input.Register(first, YYInteractionBlockFlags.GameplayActions);
            input.Register(second, YYInteractionBlockFlags.GameplayActions);
            input.SetActionMap(first.actionMap); input.Refresh(true); Step();
        }
        [TearDown]
        public override void TearDown()
        {
            try { input?.Dispose(); sessions?.Dispose(); UnityEngine.Object.DestroyImmediate(asset); }
            finally { base.TearDown(); }
        }
        private void Step(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); input.Refresh(true);
        }
        [Test]
        public void ShortPressAndReleaseAreVisibleOnce()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            Assert.That(input.CanRead(first) && first.WasPressedThisFrame(), Is.True);
            Assert.That(first.IsPressed(), Is.False);
            Step(); Assert.That(first.WasPressedThisFrame(), Is.False);
        }
        [Test]
        public void HeldButtonDoesNotBecomeASecondPressWhenSwitchingMaps()
        {
            Step(Key.Space); Assert.That(first.WasPressedThisFrame(), Is.True);
            input.SetActionMap(second.actionMap);
            Assert.That(input.CanRead(second), Is.False, "Enabling cannot expose a press from this input update.");
            Step(Key.Space); Assert.That(second.WasPressedThisFrame(), Is.False);
            Step(); Step(Key.Space); Assert.That(input.CanRead(second) && second.WasPressedThisFrame(), Is.True);
        }
        [Test]
        public void UiCloseInTheSameUpdateCannotLeakAButton()
        {
            Step(Key.Space);
            using (sessions.Begin(new YYInteractionSessionDescriptor { Kind = "modal", Blocks = YYInteractionBlockFlags.All }))
                Assert.That(input.CanRead(first), Is.False);
            Assert.That(input.CanRead(first), Is.False);
            Step(Key.Space); Assert.That(first.WasPressedThisFrame(), Is.False);
            Step(); Step(Key.Space); Assert.That(first.WasPressedThisFrame(), Is.True);
        }
        [Test]
        public void LossOfPermissionCancelsAndClearsHeldState()
        {
            Step(Key.Space); input.Refresh(false);
            Assert.That(input.CanRead(first), Is.False); Assert.That(first.IsPressed(), Is.False);
            InputSystem.ResetDevice(keyboard); InputSystem.Update(); input.Refresh(true);
            Step(); Assert.That(first.IsPressed(), Is.False);
        }
        [Test]
        public void DisposeInsideCanceledCallbackIsSafe()
        {
            Step(Key.Space);
            first.canceled += _ => input.Dispose();
            Assert.DoesNotThrow(() => input.Refresh(false));
            Assert.That(first.enabled, Is.False);
            Assert.That(input.CanRead(first), Is.False);
        }
        [TestCase(2)]
        [TestCase(32)]
        public void StableRoutingDoesNotAllocate(int count)
        {
            input.Dispose(); UnityEngine.Object.DestroyImmediate(asset);
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("Benchmark");
            var reads = new InputAction[count];
            for (int i = 0; i < count; i++) reads[i] = map.AddAction("Probe" + i, InputActionType.Button, "<Keyboard>/space");
            first = reads[0]; asset.devices = new InputDevice[] { keyboard };
            input = new YYInputActionService(sessions);
            foreach (var action in reads)
            {
                input.Register(action, YYInteractionBlockFlags.GameplayActions);
            }
            input.SetActionMap(map);
            input.Refresh(true); Step();
            for (int i = 0; i < 100; i++) { input.Refresh(true); foreach (var action in reads) input.CanRead(action); }
            var watch = new System.Diagnostics.Stopwatch();
            var recorder = UnityEngine.Profiling.Recorder.Get("GC.Alloc");
            recorder.enabled = false; recorder.FilterToCurrentThread();
            int calibration, allocations;
            try
            {
                recorder.enabled = true; GC.KeepAlive(new byte[4096]); recorder.enabled = false;
                calibration = recorder.sampleBlockCount;
                recorder.enabled = true; watch.Start();
                for (int i = 0; i < 10000; i++) { input.Refresh(true); foreach (var action in reads) input.CanRead(action); }
                watch.Stop(); recorder.enabled = false; allocations = recorder.sampleBlockCount;
            }
            finally { recorder.enabled = false; recorder.CollectFromAllThreads(); }
            Assert.That(calibration, Is.GreaterThan(0), "GC recorder must detect a retained allocation.");
            Assert.That(allocations, Is.Zero);
            TestContext.WriteLine("YYGC_ROUTING iterations=10000 registered=" + count + " gcAllocations=" + allocations +
                " elapsedMs=" + watch.Elapsed.TotalMilliseconds + " calibrationAllocations=" + calibration);
        }
    }
}
