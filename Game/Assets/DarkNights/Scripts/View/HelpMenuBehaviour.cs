using GameCore.Objects.Views;
using GameCore.UI.UGUI;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 原玩法帮助页；关闭只恢复前一层本地界面，不改变游戏状态。
    /// 按生成器绑定原生 UGUI 组件和事件，池化退出时通过基类释放用户回调。
    /// </summary>
    public sealed partial class HelpMenuBehaviour : MenuBehaviour
    {
        [UGUIOnClick("Back")] private void OnBack() => Raise("Back");
    }
}
