using GameCore.Objects.Views;
using GameCore.UI.UGUI;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 三夜结算页的只读展示；重新开局请求仍走权威生命周期入口。
    /// 按生成器绑定原生 UGUI 组件和事件，池化退出时通过基类释放用户回调。
    /// </summary>
    public sealed partial class ResultMenuBehaviour : MenuBehaviour
    {
        [ViewComponent("ResultTitle")] private Text title;
        [ViewComponent("ResultDetail")] private Text detail;
        [ViewComponent("ResultStats")] private Text stats;
        public void Present(bool won, string heading, string message, string values)
        {
            title.text = heading; detail.text = message; stats.text = values;
            title.color = won ? new UnityEngine.Color(.85f, .765f, .576f) : new UnityEngine.Color(.8745f, .6706f, .6f);
        }
        [UGUIOnClick("NewGame")] private void OnNewGame() => Raise("NewGame");
        [UGUIOnClick("MainMenu")] private void OnMainMenu() => Raise("MainMenu");
    }
}
