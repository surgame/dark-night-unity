using System;
using System.Collections.Generic;
using GameCore.Interactions;
using GameCore.PlayerInputs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.LowLevel;

namespace DarkNights.View
{
    /// <summary>
    /// 本客户端原生 PlayerInput 的缓存接线；动作、设备与 UI 继续由 Unity 管理，互斥复用 YYGC 会话。
    /// 切模式同步取消动作，失焦或设备丢失立即撤销许可；稳定帧不查找动作名、不创建射线列表。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class GameInputActions : MonoBehaviour
    {
        private PlayerInput player;
        private YYInputActionService routing;
        private IYYInteractionSessionService sessions;
        private YYInteractionSessionHandle pointerBlock;
        private PointerEventData pointer;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private InputActionMap camp, hero;
        private InputAction point;
        private bool ready, deviceLost;
        private bool menuSuppressed;
        private uint menuSuppressedAt;
        public InputAction Move { get; private set; }
        public InputAction Jump { get; private set; }
        public InputAction Drop { get; private set; }
        public InputAction UseItem { get; private set; }
        public InputAction Item1 { get; private set; }
        public InputAction Item2 { get; private set; }
        public InputAction Item3 { get; private set; }
        public InputAction Item4 { get; private set; }
        public InputAction HeroToggle { get; private set; }
        public InputAction CampToggle { get; private set; }
        public InputAction Menu { get; private set; }
        public InputAction Scroll { get; private set; }
        public bool HeroMode { get; private set; }
        public bool RebindingActive { get; set; }
        public bool CanReadMenu => Application.isFocused && !RebindingActive &&
            (!menuSuppressed || menuSuppressedAt != InputState.updateCount);
        public void SuppressMenu() { menuSuppressed = true; menuSuppressedAt = InputState.updateCount; }
        public bool PointerOverUi { get; private set; }
        public Vector2 Pointer => point?.ReadValue<Vector2>() ?? Vector2.zero;
        public event Action Unavailable;
        public InputActionAsset Asset => player.actions;
        public InputAction CampAction(string name) => camp.FindAction(name, true);
        public bool CanRead(InputAction action) => routing != null && routing.CanRead(action);

        public void Initialize(PlayerInput value, IYYInteractionSessionService service)
        {
            player = value != null ? value : throw new InvalidOperationException("Assign Pinewatch PlayerInput.");
            sessions = service;
            YYInputSettingsStore.TryApplyBindingOverrides(player.actions);
            camp = player.actions.FindActionMap("Camp", true); hero = player.actions.FindActionMap("Player", true);
            var ui = player.actions.FindActionMap("UI", true); ui.Enable();
            point = ui.FindAction("Point", true); Menu = ui.FindAction("Cancel", true); Scroll = ui.FindAction("ScrollWheel", true);
            Move = hero.FindAction("Move", true); Jump = hero.FindAction("Jump", true);
            Drop = hero.FindAction("Crouch", true); UseItem = hero.FindAction("Attack", true);
            Item1 = hero.FindAction("Item1", true); Item2 = hero.FindAction("Item2", true); Item3 = hero.FindAction("Item3", true);
            Item4 = hero.FindAction("Item4", true);
            HeroToggle = hero.FindAction("ToggleMode", true); CampToggle = camp.FindAction("ToggleMode", true);
            routing = new YYInputActionService(sessions);
            foreach (var action in camp.actions) routing.Register(action, YYInteractionBlockFlags.GameplayActions);
            foreach (var action in hero.actions) routing.Register(action, YYInteractionBlockFlags.GameplayActions |
                (action == UseItem ? YYInteractionBlockFlags.WorldConfirm : YYInteractionBlockFlags.None));
            if (EventSystem.current == null) throw new InvalidOperationException("UGUI EventSystem is required.");
            pointer = new PointerEventData(EventSystem.current);
            player.uiInputModule = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            player.onDeviceLost += DeviceLost; player.onDeviceRegained += DeviceRegained;
            SetHero(false);
        }

        public void Present(bool canSend) { ready = canSend; }
        public void SetHero(bool value)
        {
            if (routing == null) return;
            HeroMode = value;
            player.SwitchCurrentActionMap(value ? "Player" : "Camp");
            routing.SetActionMap(value ? hero : camp);
        }

        private void Update()
        {
            if (routing == null) return;
            hits.Clear(); pointer.position = Pointer;
            EventSystem.current.RaycastAll(pointer, hits);
            PointerOverUi = hits.Count != 0;
            if (PointerOverUi && pointerBlock == null)
                pointerBlock = sessions.Begin(new YYInteractionSessionDescriptor
                { Kind = "dark_nights.pointer", Owner = nameof(GameInputActions), Blocks = YYInteractionBlockFlags.WorldConfirm });
            else if (!PointerOverUi) { pointerBlock?.Dispose(); pointerBlock = null; }
            routing.Refresh(ready && Application.isFocused && !deviceLost);
        }

        private void DeviceLost(PlayerInput value) { deviceLost = true; StopInput(); }
        private void DeviceRegained(PlayerInput value) { deviceLost = false; }
        private void OnApplicationFocus(bool focused) { if (!focused) StopInput(); }
        private void OnDisable() => StopInput();
        private void StopInput() { routing?.Refresh(false); Unavailable?.Invoke(); }
        private void OnDestroy()
        {
            StopInput(); Unavailable = null;
            if (player != null) { player.onDeviceLost -= DeviceLost; player.onDeviceRegained -= DeviceRegained; }
            routing?.Dispose(); routing = null; pointerBlock?.Dispose(); pointerBlock = null;
        }
    }
}
