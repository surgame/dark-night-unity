using GameCore.Objects.Views;
using GameCore.Logging;
using GameCore.UI.UGUI;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 暂停页与会话提示；打开菜单仅占用本地输入，暂停权仍由服务端判断。
    /// 按生成器绑定原生 UGUI 组件和事件，池化退出时通过基类释放用户回调。
    /// </summary>
    public sealed partial class PauseMenuBehaviour : MenuBehaviour
    {
        [ViewComponent("SessionDetail")] private Text detail;
        [ViewComponent("SaveHint")] private Text saveHint;
        private string lastState, lastMessage;
        public void Present(string state, string message)
        {
            detail.gameObject.SetActive(false); saveHint.gameObject.SetActive(false);
            if (state != lastState) YYLogger.LogInfo("会话状态: " + state, LoggingChannel.Network);
            if (message != lastMessage) YYLogger.LogInfo("会话提示: " + message, LoggingChannel.Gameplay);
            lastState = state; lastMessage = message;
        }
        [UGUIOnClick("Resume")] private void OnResume() => Raise("Resume");
        [UGUIOnClick("Save")] private void OnSave() => Raise("Save");
        [UGUIOnClick("Load")] private void OnLoad() => Raise("Load");
        [UGUIOnClick("Slot")] private void OnSlot() => Raise("Slot");
        [UGUIOnClick("Help")] private void OnHelp() => Raise("Help");
        [UGUIOnClick("Mute")] private void OnMute() => Raise("Mute");
        [UGUIOnClick("MainMenu")] private void OnMainMenu() => Raise("MainMenu");
        [UGUIOnClick("ControlMode")] private void OnControlMode() => Raise("ControlMode");
    }
}
