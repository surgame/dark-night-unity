using DarkNights.Core.ViewData;
using GameCore.Objects.Views;
using GameCore.UI.UGUI;
using UnityEngine.UI;
using GameCore.Logging;

namespace DarkNights.View
{
    /// <summary>
    /// 主角道具栏的原生 UGUI 展示；正式入口隐藏整块常驻工具栏，避免遮挡游戏画面。
    /// 开发者营地模式仍可显示按钮以回归旧后端；展示只消费冻结副本，不修改背包或燃料。
    /// </summary>
    public sealed partial class HeroHudBehaviour : MenuBehaviour
    {
        [ViewComponent("Status")] private Text status;
        [ViewComponent("Toggle")] private Button toggleButton;
        [ViewComponent("ToggleLabel")] private Text toggleLabel;
        [ViewComponent("Item1Label")] private Text item1;
        [ViewComponent("Item2Label")] private Text item2;
        [ViewComponent("Item3Label")] private Text item3;
        [ViewComponent("Item1")] private Button button1;
        [ViewComponent("Item2")] private Button button2;
        [ViewComponent("Item3")] private Button button3;
        [ViewComponent("Item4Label")] private Text item4;
        [ViewComponent("Item4")] private Button button4;
        [ViewComponent("Rebind")] private Button rebind;
        private string lastNotice;
        private Terrain.TerrainMiningSelectorGraphic mining;

        /// <summary>在既有 YYGC 原生面板内装配纯表现子控件；不创建游戏实体或额外 UI 管理器。</summary>
        public void InitializeMiningSelector()
        {
            if (mining != null) return;
            var viewport = (UnityEngine.RectTransform)View.transform;
            viewport.anchorMin = UnityEngine.Vector2.zero;
            viewport.anchorMax = UnityEngine.Vector2.one;
            viewport.offsetMin = viewport.offsetMax = UnityEngine.Vector2.zero;
            var overlay = new UnityEngine.GameObject("MiningSelector", typeof(UnityEngine.RectTransform));
            overlay.transform.SetParent(View.transform, false);
            overlay.transform.SetAsFirstSibling();
            mining = overlay.AddComponent<Terrain.TerrainMiningSelectorGraphic>();
            mining.raycastTarget = false;
            mining.rectTransform.anchorMin = UnityEngine.Vector2.zero;
            mining.rectTransform.anchorMax = UnityEngine.Vector2.one;
            mining.rectTransform.offsetMin = mining.rectTransform.offsetMax = UnityEngine.Vector2.zero;
        }

        public void PresentMining(UnityEngine.Camera camera, UnityEngine.Vector3 center,
            Terrain.TerrainMiningSelectorSettings settings, bool visible, bool valid) =>
            mining?.Present(camera, center, settings, visible, valid);

        public void Present(ActorViewData actor, bool ready, string jump, string notice, bool allowCampControl,
            bool showToolbar = true, string miningHint = "")
        {
            if (notice != lastNotice && !string.IsNullOrEmpty(notice))
                YYLogger.LogWarning("主角操作: " + notice, LoggingChannel.Gameplay);
            lastNotice = notice;
            var toolbar = status.transform.parent.gameObject;
            bool visible = showToolbar && (actor != null || allowCampControl);
            if (toolbar.activeSelf != visible) toolbar.SetActive(visible);
            if (!visible) return;
            rebind.gameObject.SetActive(allowCampControl);
            toggleButton.gameObject.SetActive(allowCampControl);
            toggleLabel.text = actor == null ? "操控居民 [Tab]" : "营地模式 [Tab]";
            status.text = actor == null ? "正在等待服务器分配可用居民。" :
                miningHint.Length != 0 ? miningHint :
                actor.Charging ? "蓄力 " + actor.ChargeSeconds.ToString("F1") + "s · 松开左键投掷" :
                "鼠标瞄准 · 左键使用 · 1–4 切换 · Shift 加速 · " + jump + " 跳跃";
            item1.text = Slot(actor, 0, actor?.Slot0 ?? 0);
            item2.text = Slot(actor, 1, actor?.Slot1 ?? 0);
            item3.text = Slot(actor, 2, actor?.Slot2 ?? 0);
            item4.text = Slot(actor, 3, actor?.Slot3 ?? 0);
            button1.interactable = button2.interactable = button3.interactable = button4.interactable = ready && actor != null;
            rebind.interactable = ready;
        }
        private static string Slot(ActorViewData actor, int index, int item) =>
            (actor?.SelectedItem == index ? "▶ " : "") + (index + 1) + " " +
            (item switch { 1 => "手枪", 2 => "矿镐", 3 => "炸药 · " + (actor?.ExplosiveCharges ?? 0), 4 => "手电筒", _ => "空" });
        [UGUIOnClick("Toggle")] private void OnToggle() => Raise("HeroToggle");
        [UGUIOnClick("Item1")] private void OnItem1() => Raise("HeroItem0");
        [UGUIOnClick("Item2")] private void OnItem2() => Raise("HeroItem1");
        [UGUIOnClick("Item3")] private void OnItem3() => Raise("HeroItem2");
        [UGUIOnClick("Item4")] private void OnItem4() => Raise("HeroItem3");
        [UGUIOnClick("Rebind")] private void OnRebind() => Raise("HeroRebind");
    }
}
