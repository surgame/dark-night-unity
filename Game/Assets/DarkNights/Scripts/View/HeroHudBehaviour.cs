using DarkNights.Core.ViewData;
using GameCore.Objects.Views;
using GameCore.UI.UGUI;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 主角道具栏的原生 UGUI 展示；只消费冻结角色副本，按钮发出意图，不修改背包或燃料。
    /// 组件和按钮通过 YYGC 生成绑定装配，与营地 HUD 共用面板生命周期。
    /// </summary>
    public sealed partial class HeroHudBehaviour : MenuBehaviour
    {
        [ViewComponent("Status")] private Text status;
        [ViewComponent("ToggleLabel")] private Text toggle;
        [ViewComponent("Item1Label")] private Text item1;
        [ViewComponent("Item2Label")] private Text item2;
        [ViewComponent("Item3Label")] private Text item3;
        [ViewComponent("Item1")] private Button button1;
        [ViewComponent("Item2")] private Button button2;
        [ViewComponent("Item3")] private Button button3;
        [ViewComponent("Rebind")] private Button rebind;

        public void Present(ActorViewData actor, bool ready, string jump, string notice)
        {
            toggle.text = actor == null ? "操控居民 [Tab]" : "营地模式 [Tab]";
            status.text = notice.Length != 0 ? notice : actor == null ? "选中居民后按 Tab 接管，或直接接管一名空闲居民。" :
                actor.Name + " · A/D 移动 · " + jump + " 跳跃 · S 下穿 · 左键使用 · 燃料 " + actor.JetpackFuel.ToString("F1") + "s";
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
