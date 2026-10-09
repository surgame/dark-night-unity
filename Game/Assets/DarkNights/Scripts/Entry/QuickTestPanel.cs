#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading;
using GameCore.Debugging;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>主菜单的 UITK 快速测试页；按钮只提交启动意图，可用性在激活期间更新，实际任务由宿主串行执行。</summary>
    internal sealed class QuickTestPanel : IRuntimeDebugPanel
    {
        private readonly QuickTestHub host;
        private VisualElement root;
        private IVisualElementScheduledItem refresh;
        private Button launch;
        internal QuickTestPanel(QuickTestHub host) { this.host = host; }
        public VisualElement CreateView(RuntimeDebugPanelContext context)
        {
            root = new VisualElement(); root.Add(new Label("已着陆 · 矿镐"));
            launch = new Button(() => { if (host.RequestLaunch()) context.Close(); }) { text = "启动所选测试" };
            launch.AddToClassList("rdh-primary"); root.Add(launch);
            refresh = root.schedule.Execute(() => launch.SetEnabled(host != null && host.Available)).Every(150);
            refresh.Pause(); return root;
        }
        public void OnActivated(CancellationToken token) { launch.SetEnabled(host != null && host.Available); refresh.Resume(); }
        public void OnDeactivated() => refresh?.Pause();
        public void Dispose() { OnDeactivated(); root?.Clear(); }
    }
}
#endif
