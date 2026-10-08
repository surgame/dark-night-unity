#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using DarkNights.View;
using GameCore.Debugging;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>飞船调试页的 UITK 适配；复用冻结展示及既有意图，激活时定时更新，输入租约统一归 Hub。</summary>
    internal sealed class ExpeditionDebugPanel : IRuntimeDebugPanel
    {
        private readonly ExpeditionPanel source;
        private readonly Action<string> submit;
        private readonly List<Button> actions = new List<Button>();
        private Label status;
        private VisualElement root;
        private IVisualElementScheduledItem refresh;
        internal ExpeditionDebugPanel(ExpeditionPanel source, Action<string> submit)
        { this.source = source; this.submit = submit; }
        public VisualElement CreateView(RuntimeDebugPanelContext context)
        {
            root = new VisualElement(); status = new Label(); root.Add(status);
            var row = new VisualElement(); row.AddToClassList("rdh-action-row"); root.Add(row);
            for (int index = 0; index < source.Actions.Length; index++)
            {
                int selected = index;
                var button = new Button(() =>
                {
                    string command = source.Commands[selected];
                    if (command == "pilot") context.Close(); submit(command);
                });
                actions.Add(button); row.Add(button);
            }
            refresh = root.schedule.Execute(Present).Every(150); refresh.Pause(); return root;
        }
        private void Present()
        {
            status.text = source == null ? "会话已退出" : source.Status.text;
            for (int index = 0; index < actions.Count; index++)
            {
                var original = source == null ? null : source.Actions[index];
                actions[index].text = source == null ? "" : source.ActionLabel(index);
                actions[index].EnableInClassList("rdh-hidden", original == null || !original.gameObject.activeSelf);
                actions[index].SetEnabled(original != null && source.DebugAvailable && original.interactable);
            }
        }
        public void OnActivated(CancellationToken token) { Present(); refresh.Resume(); }
        public void OnDeactivated() => refresh?.Pause();
        public void Dispose() { OnDeactivated(); root?.Clear(); actions.Clear(); }
    }
}
#endif
