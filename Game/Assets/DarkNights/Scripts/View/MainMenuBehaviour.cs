using GameCore.Objects.Views;
using GameCore.UI.UGUI;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 主菜单与 LAN 地址输入；只转发用户意图，房间及存档由 Entry 装配。
    /// 按生成器绑定原生 UGUI 组件和事件，池化退出时通过基类释放用户回调。
    /// </summary>
    public sealed partial class MainMenuBehaviour : MenuBehaviour
    {
        [ViewComponent("Address")] private InputField address;
        [ViewComponent("ConnectionStatus")] private Text status;
        public string Address => address.text;
        public void ShowStatus(string value) { status.text = value; }
        [UGUIOnClick("NewGame")] private void OnNewGame() => Raise("NewGame");
        [UGUIOnClick("Continue")] private void OnContinue() => Raise("Continue");
        [UGUIOnClick("Help")] private void OnHelp() => Raise("Help");
        [UGUIOnClick("Quit")] private void OnQuit() => Raise("Quit");
        [UGUIOnClick("Join")] private void OnJoin() => Raise("Join");
    }
}
