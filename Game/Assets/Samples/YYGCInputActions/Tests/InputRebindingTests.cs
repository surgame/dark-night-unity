using System;
using System.Collections.Generic;
using GameCore.Interactions;
using GameCore.PlayerInputs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameCore.Samples.InputActions.Tests
{
    /// <summary>
    /// 输入改键的失败与生命周期回归，使用真实 InputAction 和框架服务。
    /// 每例独占临时动作资产，退出清理；不接触项目资产、玩家设置或游戏会话。
    /// </summary>
    public sealed class InputRebindingTests : InputTestFixture
    {
        private InputActionAsset asset;
        private InputAction jump;
        [SetUp]
        public override void Setup()
        {
            base.Setup();
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            jump = asset.AddActionMap("Player").AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
        }
        [TearDown]
        public override void TearDown()
        {
            try { asset?.Disable(); UnityEngine.Object.DestroyImmediate(asset); }
            finally { base.TearDown(); }
        }

        [Test]
        public void InvalidCompositeRootLeavesEnabledActionUntouched()
        {
            var move = jump.actionMap.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            move.Enable();
            Assert.Throws<ArgumentException>(() => YYInputRebindingService.StartInteractiveRebind(move, 0, null));
            Assert.That(move.enabled, Is.True);
        }

        [Test]
        public void InvalidIndexLeavesEnabledActionUntouched()
        {
            jump.Enable();
            Assert.Throws<ArgumentOutOfRangeException>(() => YYInputRebindingService.StartInteractiveRebind(jump, 8, null));
            Assert.That(jump.enabled, Is.True);
        }

        [Test]
        public void LegacyCancelRestoresActionAndCallsOnce()
        {
            int canceled = 0;
            jump.Enable();
            var operation = YYInputRebindingService.StartInteractiveRebind(jump, 0, null, () => canceled++);
            Assert.That(jump.enabled, Is.False);
            operation.Cancel(); operation.Dispose();
            Assert.That(jump.enabled, Is.True);
            Assert.That(canceled, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OwnedDisposeRespectsCurrentPermission(bool allowed)
        {
            int canceled = 0;
            bool permission = true;
            jump.Enable();
            var handle = YYInputRebindingService.StartManagedInteractiveRebind(jump, 0, () => permission, onCancel: () => canceled++);
            permission = allowed;
            handle.Dispose(); handle.Dispose();
            Assert.That(handle.IsFinished, Is.True);
            Assert.That(jump.enabled, Is.EqualTo(allowed));
            Assert.That(canceled, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentOwnedRebindIsRejectedAndCanRestartAfterRelease()
        {
            using (var first = YYInputRebindingService.StartManagedInteractiveRebind(jump, 0, () => true))
                Assert.Throws<InvalidOperationException>(() => YYInputRebindingService.StartManagedInteractiveRebind(jump, 0, () => true));
            using var next = YYInputRebindingService.StartManagedInteractiveRebind(jump, 0, () => true);
            Assert.That(next.IsFinished, Is.False);
        }

        [Test]
        public void GlobalDuplicateQueryIsPreservedAndFilteredQueryExcludesOtherMode()
        {
            var camp = asset.AddActionMap("Camp");
            var pause = camp.AddAction("Pause", InputActionType.Button, "<Keyboard>/space");
            Assert.That(YYInputRebindingService.TryFindDuplicateBinding(asset, jump, 0, out var duplicate, out int index), Is.True);
            Assert.That(duplicate, Is.SameAs(pause)); Assert.That(index, Is.Zero);
            Assert.That(YYInputRebindingService.TryFindDuplicateBinding(asset, jump, 0,
                new[] { jump.actionMap }, "Keyboard", out _, out _), Is.False);
        }

        [Test]
        public void CompositePartsCanStillBeRebound()
        {
            var move = jump.actionMap.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            using var handle = YYInputRebindingService.StartManagedInteractiveRebind(move, 1, () => true);
            Assert.That(handle.IsFinished, Is.False);
        }

        [Test]
        public void ActionRoutingUsesExistingChannelsAndDisposesCleanly()
        {
            var sessions = new YYInteractionSessionService();
            try
            {
                using var input = new YYInputActionService(sessions);
                input.Register(jump, YYInteractionBlockFlags.GameplayActions);
                input.SetActionMap(jump.actionMap); input.Refresh(true); InputSystem.Update();
                InputSystem.Update(); Assert.That(input.CanRead(jump), Is.True);
                using (sessions.Begin(new YYInteractionSessionDescriptor
                { Kind = "test", Blocks = YYInteractionBlockFlags.GameplayActions }))
                    Assert.That(jump.enabled, Is.False);
                InputSystem.Update(); Assert.That(input.CanRead(jump), Is.True);
                input.Refresh(false); Assert.That(jump.enabled, Is.False);
                input.Refresh(true); input.Dispose();
                Assert.That(jump.enabled, Is.False);
                Assert.That(input.CanRead(jump), Is.False);
            }
            finally { sessions.Dispose(); }
        }

        [Test]
        public void RoutingDoesNotEnableActionDuringManagedRebind()
        {
            var sessions = new YYInteractionSessionService();
            try
            {
                using var input = new YYInputActionService(sessions);
                input.Register(jump, YYInteractionBlockFlags.GameplayActions);
                input.SetActionMap(jump.actionMap); input.Refresh(true); InputSystem.Update();
                using var rebind = YYInputRebindingService.StartManagedInteractiveRebind(jump, 0, () => true);
                input.Refresh(true);
                Assert.That(jump.enabled, Is.False);
            }
            finally { sessions.Dispose(); }
        }

        [Test]
        public void StartupExceptionRestoresEnabledActionAndReleasesOwnership()
        {
            jump.Enable();
            Assert.Throws<InvalidOperationException>(() => YYInputRebindingService.StartManagedInteractiveRebind(
                jump, 0, () => true, excludedControls: new BrokenExclusions()));
            Assert.That(jump.enabled, Is.True);
            using var next = YYInputRebindingService.StartManagedInteractiveRebind(jump, 0, () => true);
            Assert.That(next.IsFinished, Is.False);
        }

        [Test]
        public void KeyboardCompletesOwnedRebindOnce()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            int completed = 0; jump.Enable();
            using var handle = YYInputRebindingService.StartManagedInteractiveRebind(jump, 0, () => true, () => completed++);
            Press(keyboard.kKey); currentTime += .1; InputSystem.Update();
            Assert.That(handle.IsFinished, Is.True);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/k"));
        }

        /// <summary>在原生操作创建后制造配置读取异常，验证真正的启动异常恢复路径。</summary>
        private sealed class BrokenExclusions : IReadOnlyList<string>
        {
            public int Count => 1;
            public string this[int index] => throw new InvalidOperationException("Expected configuration read failure.");
            public IEnumerator<string> GetEnumerator() => throw new NotSupportedException();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
