using System;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>
    /// 导航面板内的个人坐标／说明对话框；输入仅生成预览航点，确认航行仍由外层按钮完成。
    /// 本框不获取第二份交互会话，关闭面板时清理，坐标限制沿用现有星图正负4096范围。
    /// </summary>
    internal sealed class ShipNavigationDialog : IDisposable
    {
        private readonly VisualElement backdrop, fields;
        private readonly Label title, note, error;
        private readonly IntegerField x, y;
        private readonly Button accept, cancel;
        private bool help;
        public event Action<float, float> Accepted;

        internal ShipNavigationDialog(VisualElement root)
        {
            backdrop = root.Q("navigation-dialog-backdrop"); fields = root.Q("navigation-coordinate-fields");
            title = root.Q<Label>("navigation-dialog-title"); note = root.Q<Label>("navigation-dialog-note");
            error = root.Q<Label>("navigation-coordinate-error");
            x = root.Q<IntegerField>("navigation-coordinate-x"); y = root.Q<IntegerField>("navigation-coordinate-y");
            accept = root.Q<Button>("navigation-dialog-accept"); cancel = root.Q<Button>("navigation-dialog-cancel");
            accept.clicked += Confirm; cancel.clicked += Close;
            backdrop.RegisterCallback<KeyDownEvent>(KeyDown);
        }

        internal void Open(bool showHelp)
        {
            help = showHelp; error.text = "";
            title.text = help ? "星图浏览器 - 使用说明" : "指定航行坐标";
            note.text = help ? "单击星体查阅资料；在迷雾中单击指定航点。\n按住鼠标拖动星图，使用滚轮缩放。\n本船实际经过的区域会记入共享星图，移开后仍可查阅。\n星体目录仅列出已登记对象，不显示星域总数。\n坐标抵达后在太空待命，示意星体不开放降落。" :
                "请输入目的地的星域坐标。\n有效范围: −4096 至 +4096。未勘测区域暂无星体资料。";
            fields.EnableInClassList("is-unavailable", help);
            backdrop.AddToClassList("is-open");
            if (help) accept.Focus(); else x.Focus();
        }
        private void Confirm()
        {
            if (help) { Close(); return; }
            if (x.value < -4096 || x.value > 4096 || y.value < -4096 || y.value > 4096)
            { error.text = "坐标超出有效范围，请重新输入。"; return; }
            Accepted?.Invoke(x.value, y.value); Close();
        }
        private void KeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != UnityEngine.KeyCode.Escape) return;
            Close(); evt.StopPropagation();
        }
        internal void Close() => backdrop.RemoveFromClassList("is-open");
        public void Dispose()
        {
            accept.clicked -= Confirm; cancel.clicked -= Close; Accepted = null;
            backdrop.UnregisterCallback<KeyDownEvent>(KeyDown); Close();
        }
    }
}
