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
                "1/2/3 或滚轮选道具 · 左键使用 · 装备喷气背包后空中按住跳跃\n" +
                "进入房间后由服务器直接分配一名可用居民；旧营地操控入口暂时隐藏\n" +
                "房主：P 暂停 · Esc 菜单 · F5/F9 存取";
        }

        [UGUIOnClick("Back")] private void OnBack() => Raise("Back");
    }
}
