using GameCore.Objects.Views;
using GameCore.UI.UGUI;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 当前主角操控的操作说明；关闭只恢复前一层本地界面，不改变游戏状态。
    /// 按生成器绑定原生 UGUI 组件和事件，池化退出时通过基类释放用户回调。
    /// </summary>
    public sealed partial class HelpMenuBehaviour : MenuBehaviour
    {
        [ViewComponent("ControlsGuide")] private Text controls;
        public void Present(string jump)
        {
            controls.text = "主角：A/D 移动 · " + jump + " 跳跃 · S 下穿\n" +
                "1 手枪 · 2 矿镐 · 3 炸药 · 4 背包 · 滚轮切换 · 鼠标瞄准\n左键射击 / 挥镐 · 炸药按住蓄力松开投掷 · 背包左键装备，空中按住跳跃\n" +
                "进入房间后由服务器直接分配一名可用居民；旧营地操控入口暂时隐藏\n" +
                "房主：P 暂停 · Esc 菜单 · F5/F9 存取";
        }

        [UGUIOnClick("Back")] private void OnBack() => Raise("Back");
    }
}
