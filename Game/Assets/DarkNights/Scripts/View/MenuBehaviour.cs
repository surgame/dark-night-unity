using System;
using GameCore.UI.UGUI;

namespace DarkNights.View
{
    /// <summary>
    /// UGUI 菜单的局部意图订阅寿命；事件只连接当前面板和 Entry 控制器，不是全局业务路由。
    /// 框架负责生成绑定和池化，离开面板实例时清除订阅，避免重开菜单重复执行命令。
    /// </summary>
    public abstract class MenuBehaviour : UGUIBehaviour
    {
        public event Action<string> Action;
        protected void Raise(string action) { Action?.Invoke(action); }
        public override void OnDespawn() { Action = null; base.OnDespawn(); }
    }
}
