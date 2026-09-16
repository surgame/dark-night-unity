using GameCore.Objects.Views;
using GameCore.UI.UGUI;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 原玩法帮助页及当前两种控制模式的操作说明；关闭只恢复前一层本地界面，不改变游戏状态。
    /// 按生成器绑定原生 UGUI 组件和事件，池化退出时通过基类释放用户回调。
    /// </summary>
    public sealed partial class HelpMenuBehaviour : MenuBehaviour
    {
        [ViewComponent("ControlsGuide")] private Text controls;
        public void Present(string jump)
        {
            controls.text = "主角：A/D 移动 · " + jump + " 跳跃 · S 下穿 · Tab 切换营地\n" +
                "1/2/3 或滚轮选道具 · 左键使用 · 装备喷气背包后空中按住跳跃\n" +
                "营地：左键选择 / 框选 · Shift 追加 · 右键执行 · A/D 镜头 · 滚轮缩放\n" +
                "营地：G 守卫 · I 工人 · Home 回营地 · P 暂停 · Esc 菜单 · F5/F9 存取";
        }

        [UGUIOnClick("Back")] private void OnBack() => Raise("Back");
    }
}
