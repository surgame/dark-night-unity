using System;
using System.Collections.Generic;
using GameCore.Interactions;
using GameCore.PlayerInputs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 每位本地玩家唯一的游戏输入接入点。原生动作和 YYGC 许可留在此处；业务只取得当前帧的语义值。
    /// 改键复用原资产的动作／绑定身份，不保存第二份按键状态。
    /// </summary>
    [DefaultExecutionOrder(-1300)]
    public sealed class GameInputActions : MonoBehaviour
    {
        /// <summary>一帧主角操作的只读值；边沿缓冲和业务解释仍由主角采样器负责。</summary>
        public readonly struct HeroFrame
        {
            public readonly float Move;
            public readonly Vector2 Pointer;
            public readonly bool Allowed, JumpHeld, JumpPressed, DropHeld, DropPressed, SprintHeld, InteractPressed;
            public readonly bool UseAllowed, UseHeld, UsePressed, UseReleased;
            public readonly int ItemPressed;

            internal HeroFrame(GameInputActions source)
            {
                Allowed = source.CanRead(source.move);
                Move = Allowed ? source.move.ReadValue<float>() : 0;
                JumpHeld = Allowed && source.CanRead(source.jump) && source.jump.IsPressed();
                JumpPressed = Allowed && source.CanRead(source.jump) && source.jump.WasPressedThisFrame();
                DropHeld = Allowed && source.CanRead(source.drop) && source.drop.IsPressed();
                DropPressed = Allowed && source.CanRead(source.drop) && source.drop.WasPressedThisFrame();
                SprintHeld = Allowed && source.CanRead(source.sprint) && source.sprint.IsPressed();
                InteractPressed = Allowed && source.Pressed(source.interact);
                UseAllowed = Allowed && source.CanRead(source.useItem);
                UseHeld = UseAllowed && source.useItem.IsPressed();
                UsePressed = UseAllowed && source.useItem.WasPressedThisFrame();
                UseReleased = UseAllowed && source.useItem.WasReleasedThisFrame();
                ItemPressed = !Allowed ? -1 : source.Pressed(source.item1) ? 0 : source.Pressed(source.item2) ? 1 :
                    source.Pressed(source.item3) ? 2 : source.Pressed(source.item4) ? 3 : -1;
                Pointer = source.Pointer;
            }
        }
        /// <summary>一帧营地操作的只读值；本地选择、建造和镜头规则仍归营地视图。</summary>
        public readonly struct CampFrame
        {
            public readonly Vector2 Pointer, PanDelta;
            public readonly float CameraMove, Scroll;
            public readonly bool Menu, Allowed, CameraAllowed, PointerOverUi;
            public readonly bool Pause, Help, Save, Load, Home, Guards, Idle;
            public readonly bool Orders, Select, SelectHeld, AppendHeld, PanHeld;

            internal CampFrame(GameInputActions source)
            {
                Menu = source.CanReadMenu && source.menu.WasPressedThisFrame();
                Allowed = source.CanRead(source.cameraMove);
                CameraAllowed = !source.sessions.IsBlocked(YYInteractionBlockFlags.CameraInput);
                Pointer = source.Pointer;
                PointerOverUi = source.PointerOverUi;
                Pause = Allowed && source.Pressed(source.pause);
                Help = Allowed && source.Pressed(source.help);
                Save = Allowed && source.Pressed(source.save);
                Load = Allowed && source.Pressed(source.load);
                Home = Allowed && source.Pressed(source.home);
                Guards = Allowed && source.Pressed(source.guards);
                Idle = Allowed && source.Pressed(source.idle);
                Orders = Allowed && source.Pressed(source.orders);
                Select = Allowed && source.Pressed(source.select);
                SelectHeld = Allowed && source.select.IsPressed();
                AppendHeld = Allowed && source.append.IsPressed();
                PanHeld = Allowed && source.pan.IsPressed();
                CameraMove = Allowed && CameraAllowed ? source.cameraMove.ReadValue<float>() : 0;
                PanDelta = Allowed && CameraAllowed ? source.panDelta.ReadValue<Vector2>() : Vector2.zero;
                Scroll = Allowed && CameraAllowed && !PointerOverUi ? source.scroll.ReadValue<Vector2>().y : 0;
            }
        }
        /// <summary>设置界面可显示的原生绑定身份；显示名不复制默认键位或运行值。</summary>
        public readonly struct BindingInfo
        {
            public readonly Guid ActionId, BindingId;
            public readonly string Name, Current;
            internal BindingInfo(Guid actionId, Guid bindingId, string name, string current)
            {
                ActionId = actionId; BindingId = bindingId;
                Name = name; Current = current;
            }
        }
        private PlayerInput player;
        private YYInputActionService routing;
        private IYYInteractionSessionService sessions;
        private YYInteractionSessionHandle pointerBlock, rebindModal;
        private YYInputRebindingHandle rebind;
        private PointerEventData pointer;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private InputActionMap camp, hero;
        private InputAction point, menu, scroll, move, jump, drop, sprint, interact, useItem;
        private InputAction item1, item2, item3, item4, heroToggle, campToggle;
        private InputAction cameraMove, select, orders, append, pan, panDelta;
        private InputAction pause, help, save, load, home, guards, idle;
        private bool ready, deviceLost, rebinding, menuSuppressed;
        private uint menuSuppressedAt;
        public bool HeroMode { get; private set; }
        public bool IsRebinding => rebinding;
        public bool PointerOverUi { get; private set; }
        public Vector2 Pointer => point?.ReadValue<Vector2>() ?? Vector2.zero;
        public string JumpBindingLabel => YYInputRebindingService.GetBindingDisplayString(jump, 0);
        public event Action Unavailable;

        private bool CanReadMenu => Application.isFocused && !rebinding &&
            (!menuSuppressed || menuSuppressedAt != InputState.updateCount);
        private bool CanRead(InputAction action) => routing != null && routing.CanRead(action);
        private bool Pressed(InputAction action) => CanRead(action) && action.WasPressedThisFrame();

        public void Initialize(PlayerInput value, IYYInteractionSessionService service)
        {
            player = value != null ? value : throw new InvalidOperationException("Assign Pinewatch PlayerInput.");
            sessions = service ?? throw new ArgumentNullException(nameof(service));
            YYInputSettingsStore.TryApplyBindingOverrides(player.actions);
            camp = player.actions.FindActionMap("Camp", true); hero = player.actions.FindActionMap("Player", true);
            var ui = player.actions.FindActionMap("UI", true); ui.Enable();
            point = ui.FindAction("Point", true); menu = ui.FindAction("Cancel", true);
            scroll = ui.FindAction("ScrollWheel", true);
            move = hero.FindAction("Move", true); jump = hero.FindAction("Jump", true);
            drop = hero.FindAction("Crouch", true); useItem = hero.FindAction("Attack", true);
            sprint = hero.FindAction("Sprint", true); interact = hero.FindAction("Interact", true);
            item1 = hero.FindAction("Item1", true); item2 = hero.FindAction("Item2", true);
            item3 = hero.FindAction("Item3", true); item4 = hero.FindAction("Item4", true);
            heroToggle = hero.FindAction("ToggleMode", true); campToggle = camp.FindAction("ToggleMode", true);
            cameraMove = camp.FindAction("Move", true); select = camp.FindAction("Select", true);
            orders = camp.FindAction("Orders", true); append = camp.FindAction("Append", true);
            pan = camp.FindAction("Pan", true); panDelta = camp.FindAction("PanDelta", true);
            pause = camp.FindAction("Pause", true); help = camp.FindAction("Help", true);
            save = camp.FindAction("Save", true); load = camp.FindAction("Load", true);
            home = camp.FindAction("Home", true); guards = camp.FindAction("Guards", true);
            idle = camp.FindAction("IdleWorkers", true);
            routing = new YYInputActionService(sessions);
            foreach (var action in camp.actions) routing.Register(action, YYInteractionBlockFlags.GameplayActions);
            foreach (var action in hero.actions) routing.Register(action, YYInteractionBlockFlags.GameplayActions |
                (action == useItem ? YYInteractionBlockFlags.WorldConfirm : YYInteractionBlockFlags.None));
            if (EventSystem.current == null) throw new InvalidOperationException("UGUI EventSystem is required.");
            pointer = new PointerEventData(EventSystem.current);
            player.uiInputModule = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            player.onDeviceLost += DeviceLost; player.onDeviceRegained += DeviceRegained;
            SetHero(false);
        }
        public HeroFrame ReadHero() => new HeroFrame(this);
        public CampFrame ReadCamp() => new CampFrame(this);
        public bool ModeTogglePressed => Pressed(HeroMode ? heroToggle : campToggle);
        public void Present(bool canSend) { ready = canSend; }
        public void SetHero(bool value)
        {
            if (routing == null) return;
            HeroMode = value;
            player.SwitchCurrentActionMap(value ? "Player" : "Camp");
            routing.SetActionMap(value ? hero : camp);
        }
        /// <summary>只枚举游戏动作中可改的实体绑定；复合绑定根节点不参与改键。</summary>
        public IReadOnlyList<BindingInfo> GetBindings() => GameInputBindingCatalog.Read(hero, camp, Rebindable);

        public void StartRebind(BindingInfo target, Action<string> finished)
        {
            if (rebinding) throw new InvalidOperationException("A rebind is already active.");
            if (!TryResolve(target, out InputAction action, out int index))
                throw new ArgumentException("Unknown rebindable binding.", nameof(target));
            string previous = action.bindings[index].overridePath;
            rebinding = true;
            try
            {
                rebindModal = sessions.Begin(new YYInteractionSessionDescriptor
                { Kind = "dark_nights.rebind", Owner = nameof(GameInputActions), Priority = 200, Blocks = YYInteractionBlockFlags.All });
                rebind = YYInputRebindingService.StartManagedInteractiveRebind(action, index, () => false,
                    () => FinishRebind(action, index, previous, true, finished),
                    () => FinishRebind(action, index, previous, false, finished));
            }
            catch { rebinding = false; rebindModal?.Dispose(); rebindModal = null; throw; }
        }

        /// <summary>按原生绑定 ID 恢复单项默认值，并保存当前全部覆盖设置。</summary>
        public bool ResetBinding(BindingInfo target)
        {
            if (rebinding) return false;
            if (!TryResolve(target, out InputAction action, out int index))
                throw new ArgumentException("Unknown binding.", nameof(target));
            YYInputRebindingService.ResetBinding(action, index);
            return YYInputSettingsStore.SaveBindingOverrides(player.actions);
        }

        /// <summary>恢复本地全部默认键位，仍使用 YYGC 的同一份设置文件。</summary>
        public bool ResetAllBindings()
        {
            if (rebinding) return false;
            YYInputRebindingService.ResetAllBindings(player.actions);
            return YYInputSettingsStore.SaveBindingOverrides(player.actions);
        }

        private void FinishRebind(InputAction action, int index, string previous, bool completed, Action<string> finished)
        {
            rebind = null; rebindModal?.Dispose(); rebindModal = null; rebinding = false;
            menuSuppressed = true; menuSuppressedAt = InputState.updateCount;
            string message = "已取消改键。";
            if (completed)
            {
                // UI 的左键点击与世界交互有意复用；游戏动作只与同模式动作互斥。
                var simultaneous = new[] { action.actionMap };
                if (string.Equals(action.bindings[index].effectivePath, "<Keyboard>/escape", StringComparison.OrdinalIgnoreCase) ||
                    YYInputRebindingService.TryFindDuplicateBinding(player.actions, action, index, simultaneous,
                    null, out _, out _))
                {
                    if (previous == null) YYInputRebindingService.ResetBinding(action, index);
                    else YYInputRebindingService.ApplyBindingOverride(action, index, previous);
                    message = "该按键已被同时使用的动作占用。";
                }
                else message = YYInputSettingsStore.SaveBindingOverrides(player.actions) ? "按键已保存。" : "按键已生效，但保存失败。";
            }
            finished?.Invoke(message);
        }

        public void CancelRebind() => rebind?.Dispose();
        private bool TryResolve(BindingInfo target, out InputAction action, out int index)
        {
            action = null; index = -1;
            foreach (var map in new[] { hero, camp })
                foreach (var candidate in map.actions)
                    if (candidate.id == target.ActionId && Rebindable(candidate))
                    {
                        action = candidate;
                        for (int i = 0; i < candidate.bindings.Count; i++)
                            if (candidate.bindings[i].id == target.BindingId && !candidate.bindings[i].isComposite)
                                index = i;
                        return index >= 0;
                    }
            return false;
        }

        private bool Rebindable(InputAction action) => action != null &&
            (action == move || action == jump || action == drop || action == sprint || action == interact || action == useItem ||
             action == item1 || action == item2 || action == item3 || action == item4 || action == heroToggle ||
             action == cameraMove || action == select || action == orders || action == append || action == pan ||
             action == pause || action == help || action == save || action == load || action == home ||
             action == guards || action == idle || action == campToggle);

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
        private void OnDisable() { CancelRebind(); StopInput(); }
        private void StopInput() { routing?.Refresh(false); Unavailable?.Invoke(); }
        private void OnDestroy()
        {
            CancelRebind(); StopInput(); Unavailable = null;
            if (player != null) { player.onDeviceLost -= DeviceLost; player.onDeviceRegained -= DeviceRegained; }
            routing?.Dispose(); routing = null; pointerBlock?.Dispose(); pointerBlock = null;
        }
    }
}
