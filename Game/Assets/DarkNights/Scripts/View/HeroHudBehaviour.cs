using DarkNights.Core.ViewData;
using GameCore.Objects.Views;
using GameCore.UI.UGUI;
using UnityEngine.UI;

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
        [ViewComponent("Rebind")] private Button rebind;

        public void Present(ActorViewData actor, bool ready, string jump, string notice, bool allowCampControl)
        {
            var toolbar = status.transform.parent.gameObject;
            if (toolbar.activeSelf != allowCampControl) toolbar.SetActive(allowCampControl);
            if (!allowCampControl) return;
            toggleButton.gameObject.SetActive(allowCampControl);
            toggleLabel.text = actor == null ? "操控居民 [Tab]" : "营地模式 [Tab]";
            status.text = notice.Length != 0 ? notice : actor == null ? "正在等待服务器分配可用居民。" :
                actor.Name + " · A/D 移动 · " + jump + " 跳跃 · S 下穿 · 左键使用 · 燃料 " + actor.JetpackFuel.ToString("F1") +
                "s · 炸药 " + actor.ExplosiveCharges;
            item1.text = (actor?.SelectedItem == 0 ? "▶ " : "") + "1 职业武器";
            item2.text = (actor?.SelectedItem == 1 ? "▶ " : "") + "2 工作工具";
            item3.text = (actor?.SelectedItem == 2 ? "▶ " : "") + "3 喷气背包" + (actor?.JetpackEquipped == true ? " · 已装备" : "");
            button1.interactable = button2.interactable = button3.interactable = ready && actor != null;
            rebind.interactable = ready;
        }
        [UGUIOnClick("Toggle")] private void OnToggle() => Raise("HeroToggle");
        [UGUIOnClick("Item1")] private void OnItem1() => Raise("HeroItem0");
        [UGUIOnClick("Item2")] private void OnItem2() => Raise("HeroItem1");
        [UGUIOnClick("Item3")] private void OnItem3() => Raise("HeroItem2");
        [UGUIOnClick("Rebind")] private void OnRebind() => Raise("HeroRebind");
    }
}
