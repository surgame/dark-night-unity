using System;
using System.Collections.Generic;
using GameCore.Objects.Views;
using GameCore.UI.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 操作说明和本地按键设置；界面只使用输入层给出的绑定身份与显示名。
    /// 按键列表复用既有原生按钮样式，面板关闭时取消正在进行的交互式改键。
    /// </summary>
    public sealed partial class HelpMenuBehaviour : MenuBehaviour
    {
        [ViewComponent("ControlsGuide")] private Text controls;
        private GameInputActions actions;
        private RectTransform overlay;
        private Text status;
        private readonly Button[] rows = new Button[8];
        private Button previous, next, rebind, resetOne, resetAll, close;
        private IReadOnlyList<GameInputActions.BindingInfo> bindings;
        private GameInputActions.BindingInfo? selected;
        private int page;

        /// <summary>复用帮助面板按钮样式，只创建一组本地设置控件，不新增输入资产或事件总线。</summary>
        public void Configure(GameInputActions source, Button template)
        {
            actions = source ?? throw new ArgumentNullException(nameof(source));
            if (overlay != null) return;
            var parent = template.transform.parent as RectTransform;
            var back = (RectTransform)template.transform;
            back.anchoredPosition = new Vector2(153, 23);
            back.sizeDelta = new Vector2(300, 46);
            Button open = MakeButton(template, parent, "按键设置", 459, 23, 300, 46, OpenSettings);
            open.transform.SetAsLastSibling();

            var root = new GameObject("InputSettings", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay = (RectTransform)root.transform;
            overlay.SetParent(parent, false);
            overlay.anchorMin = overlay.anchorMax = new Vector2(.5f, .5f);
            overlay.sizeDelta = new Vector2(612, 533);
            overlay.GetComponent<Image>().color = new Color(.10f, .14f, .17f, 1);
            MakeText("设置按键 · 先选动作，再改键或恢复默认", 0, 237, 570, 32, 19);
            for (int i = 0; i < rows.Length; i++)
                rows[i] = MakeButton(template, overlay, "", 0, 184 - i * 43, 566, 39, null);
            status = MakeText("", 0, -177, 570, 32, 15);
            previous = MakeButton(template, overlay, "上一页", -250, -229, 94, 40, () => { page--; Refresh(); });
            next = MakeButton(template, overlay, "下一页", -150, -229, 94, 40, () => { page++; Refresh(); });
            rebind = MakeButton(template, overlay, "改键", -50, -229, 94, 40, BeginRebind);
            resetOne = MakeButton(template, overlay, "恢复此项", 50, -229, 94, 40, ResetSelected);
            resetAll = MakeButton(template, overlay, "全部恢复", 150, -229, 94, 40, ResetAll);
            close = MakeButton(template, overlay, "返回", 250, -229, 94, 40, CloseSettings);
            overlay.gameObject.SetActive(false);
        }

        public void Present()
        {
            controls.text = "主角：移动、跳跃、下穿 · 道具选择 · 鼠标瞄准\n" +
                "使用道具可射击 / 挥镐；炸药按住蓄力松开投掷；背包可装备道具\n" +
                "进入房间后由服务器直接分配一名可用居民；旧营地操控入口暂时隐藏\n" +
                "房主可暂停及存取进度。打开按键设置查看或修改当前键位。";
        }

        private Button MakeButton(Button template, RectTransform parent, string label,
            float x, float y, float width, float height, Action clicked)
        {
            Button button = UnityEngine.Object.Instantiate(template, parent);
            button.gameObject.name = "InputSettings_" + label;
            button.onClick.RemoveAllListeners();
            if (clicked != null) button.onClick.AddListener(() => clicked());
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            button.GetComponentInChildren<Text>().text = label;
            return button;
        }

        private Text MakeText(string label, float x, float y, float width, float height, int size)
        {
            var node = new GameObject("InputSettingsText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = (RectTransform)node.transform;
            rect.SetParent(overlay, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            var value = node.GetComponent<Text>();
            value.font = controls.font;
            value.fontSize = size;
            value.alignment = TextAnchor.MiddleCenter;
            value.color = new Color(.92f, .88f, .76f);
            value.raycastTarget = false;
            value.text = label;
            return value;
        }

        public void OpenSettings()
        {
            page = 0;
            selected = null;
            status.text = "选择动作，再选择改键或恢复此项。";
            overlay.gameObject.SetActive(true);
            Refresh();
        }

        private void Refresh()
        {
            bindings = actions.GetBindings();
            page = Mathf.Clamp(page, 0, Mathf.Max(0, (bindings.Count - 1) / rows.Length));
            for (int i = 0; i < rows.Length; i++)
            {
                int index = page * rows.Length + i;
                rows[i].gameObject.SetActive(index < bindings.Count);
                if (index >= bindings.Count) continue;
                var binding = bindings[index];
                rows[i].GetComponentInChildren<Text>().text =
                    (selected.HasValue && selected.Value.BindingId == binding.BindingId ? "▶ " : "") +
                    binding.Name + "  ·  " + binding.Current;
                rows[i].onClick.RemoveAllListeners();
                rows[i].onClick.AddListener(() => Select(binding));
                rows[i].interactable = !actions.IsRebinding;
            }
            previous.interactable = page > 0 && !actions.IsRebinding;
            next.interactable = (page + 1) * rows.Length < bindings.Count && !actions.IsRebinding;
            rebind.interactable = selected.HasValue && !actions.IsRebinding;
            resetOne.interactable = selected.HasValue && !actions.IsRebinding;
            resetAll.interactable = !actions.IsRebinding;
            close.interactable = !actions.IsRebinding;
        }

        private void Select(GameInputActions.BindingInfo binding)
        {
            selected = binding;
            status.text = "已选择 " + binding.Name + "。";
            Refresh();
        }

        private void BeginRebind()
        {
            if (!selected.HasValue) return;
            status.text = "请按新键，Esc 取消。";
            try
            {
                actions.StartRebind(selected.Value, message => { status.text = message; Refresh(); });
                Refresh();
            }
            catch (Exception error) { status.text = error.Message; Refresh(); }
        }

        private void ResetSelected()
        {
            if (!selected.HasValue) return;
            status.text = actions.ResetBinding(selected.Value) ? "已恢复并保存。" : "已恢复，但保存失败。";
            Refresh();
        }

        private void ResetAll()
        {
            status.text = actions.ResetAllBindings() ? "全部默认按键已保存。" : "已恢复默认按键，但保存失败。";
            Refresh();
        }

        public void CloseSettings()
        {
            if (overlay == null || !overlay.gameObject.activeSelf) return;
            actions.CancelRebind();
            overlay.gameObject.SetActive(false);
            Present();
        }

        [UGUIOnClick("Back")] private void OnBack()
        {
            if (overlay != null && overlay.gameObject.activeSelf) CloseSettings();
            else Raise("Back");
        }

        public override void OnDespawn() { CloseSettings(); base.OnDespawn(); }
    }
}
