using System;
using System.IO;
using System.Collections.Generic;
using GameCore.Interactions;
using GameCore.PlayerInputs;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameCore.Samples.InputActions
{
    /// <summary>
    /// 输入 Sample 的本地展示控制器；使用真实 InputAction、YYGC 仲裁和改键持久化。
    /// 演示角色只是输入反馈，不包含网络或业务状态；退出后释放全部会话和受托管改键。
    /// </summary>
    public sealed class InputActionsSample : MonoBehaviour
    {
        public PlayerInput Player;
        public Transform Actor;
        public Text Status;
        public Text BindingLabel;
        public GameObject ModalPanel;
        public Button ModeButton, ModalButton, CloseButton, RebindButton, CancelButton, SaveButton, LoadButton;
        public string SettingsFile = "yy_input_sample_settings.json";
        private YYInteractionSessionService sessions;
        private YYInputActionService input;
        private YYInteractionSessionHandle modal;
        private YYInteractionSessionHandle pointerBlock;
        private PointerEventData pointer;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private YYInputRebindingHandle rebind;
        private InputAction move, jump, use, point;
        private bool camp, focused;
        private float velocity;
        private int jumps, presses, releases;
        private string notice = "Space jumps; left mouse uses the selected demo tool.";
        public string SettingsPath => Path.Combine(Application.persistentDataPath, SettingsFile);
        public int JumpCount => jumps;
        public int UseCount => presses;

        private void OnEnable()
        {
            focused = Application.isFocused;
            sessions = new YYInteractionSessionService();
            input = new YYInputActionService(sessions);
            pointer = new PointerEventData(EventSystem.current);
            point = Player.actions.FindAction("UI/Point", true);
            foreach (string mapName in new[] { "Player", "Camp" })
                foreach (var action in Player.actions.FindActionMap(mapName).actions)
                    input.Register(action, YYInteractionBlockFlags.GameplayActions |
                        (action.name == "UseItem" ? YYInteractionBlockFlags.WorldConfirm : YYInteractionBlockFlags.None));
            ModeButton.onClick.AddListener(ToggleMode);
            ModalButton.onClick.AddListener(OpenModal);
            CloseButton.onClick.AddListener(CloseModal);
            CancelButton.onClick.AddListener(CancelRebind);
            RebindButton.onClick.AddListener(RebindJump);
            SaveButton.onClick.AddListener(Save);
            LoadButton.onClick.AddListener(Load);
            SetMode();
            UpdateLabel();
            Status.transform.SetAsLastSibling();
            BindingLabel.transform.SetAsLastSibling();
        }

        private void ToggleMode() { camp = !camp; SetMode(); }
        private void SetMode()
        {
            Player.SwitchCurrentActionMap(camp ? "Camp" : "Player");
            var map = Player.currentActionMap;
            move = map.FindAction("Move", true);
            jump = map.FindAction("Jump", true);
            use = map.FindAction("UseItem", true);
            input.SetActionMap(map);
            UpdateLabel();
        }

        private void Update()
        {
            if (input == null) return;
            pointer.position = point.ReadValue<Vector2>();
            hits.Clear(); EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count != 0 && pointerBlock == null)
                pointerBlock = sessions.Begin(new YYInteractionSessionDescriptor
                { Kind = "input_sample.pointer", Blocks = YYInteractionBlockFlags.WorldConfirm });
            else if (hits.Count == 0) { pointerBlock?.Dispose(); pointerBlock = null; }
            input.Refresh(focused);
            if (input.CanRead(move)) Actor.position += Vector3.right * move.ReadValue<float>() * Time.deltaTime * 3;
            if (input.CanRead(jump) && jump.WasPressedThisFrame() && Actor.position.y <= -1.49f)
            {
                velocity = 5;
                jumps++;
            }
            velocity -= 12 * Time.deltaTime;
            Vector3 position = Actor.position;
            position.x = Mathf.Clamp(position.x, -7, 7);
            position.y = Mathf.Max(-1.5f, position.y + velocity * Time.deltaTime);
            if (position.y <= -1.5f) velocity = 0;
            Actor.position = position;
            if (input.CanRead(use) && use.WasPressedThisFrame()) presses++;
            if (use.WasReleasedThisFrame()) releases++;
            bool held = input.CanRead(use) && use.IsPressed();
            Status.text = (camp ? "CAMP" : "PLAYER") + "   jumps " + jumps + "   use " + presses + "/" + releases +
                "   held " + held + "\n" + notice;
            RebindButton.interactable = rebind == null;
            CancelButton.interactable = rebind != null;
        }

        public void OpenModal()
        {
            if (modal != null) return;
            modal = sessions.Begin(new YYInteractionSessionDescriptor
            {
                Kind = "input_sample.modal", Owner = nameof(InputActionsSample), Priority = 100,
                Blocks = YYInteractionBlockFlags.All
            });
            ModalPanel.SetActive(true);
        }

        public void CloseModal()
        {
            CancelRebind();
            ModalPanel.SetActive(false);
            modal?.Dispose();
            modal = null;
        }

        public void RebindJump()
        {
            if (rebind != null) return;
            OpenModal();
            notice = "Press a new jump key. Escape or Cancel restores the previous binding.";
            rebind = YYInputRebindingService.StartManagedInteractiveRebind(jump, 0, () => false,
                () => RebindFinished("Binding changed."), () => RebindFinished("Rebind canceled."));
        }

        private void RebindFinished(string value)
        {
            rebind = null;
            notice = value;
            UpdateLabel();
        }

        public void CancelRebind() { rebind?.Dispose(); rebind = null; }
        private void OnApplicationFocus(bool value)
        {
            focused = value;
            input?.Refresh(value);
        }
        public void Save() { notice = YYInputSettingsStore.SaveBindingOverrides(Player.actions, SettingsPath) ? "Saved: " + SettingsPath : "Save failed; original settings retained."; }
        public void Load()
        {
            CancelRebind();
            notice = YYInputSettingsStore.TryApplyBindingOverrides(Player.actions, SettingsPath) ? "Bindings loaded." : "Load failed; current bindings retained.";
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (jump != null) BindingLabel.text = "Jump: " + YYInputRebindingService.GetBindingDisplayString(jump, 0);
        }

        private void OnDisable()
        {
            CancelRebind();
            ModalPanel.SetActive(false);
            ModeButton.onClick.RemoveListener(ToggleMode);
            ModalButton.onClick.RemoveListener(OpenModal);
            CloseButton.onClick.RemoveListener(CloseModal);
            CancelButton.onClick.RemoveListener(CancelRebind);
            RebindButton.onClick.RemoveListener(RebindJump);
            SaveButton.onClick.RemoveListener(Save);
            LoadButton.onClick.RemoveListener(Load);
            input?.Dispose(); input = null;
            modal?.Dispose(); modal = null;
            pointerBlock?.Dispose(); pointerBlock = null;
            sessions?.CancelAll("SampleDisabled");
            sessions?.Dispose(); sessions = null;
        }
    }
}
