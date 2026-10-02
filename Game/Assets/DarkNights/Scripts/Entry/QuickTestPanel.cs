#if UNITY_EDITOR || DEVELOPMENT_BUILD
using GameCore.Debugging;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>主菜单 Hub 中的快速测试 GUI；显示当前测试与一次性启动按钮，实际任务由宿主 Update 执行。</summary>
    internal sealed class QuickTestPanel : IRuntimeDebugPanel, IRuntimeDebugPanelSizeProvider
    {
        private readonly QuickTestHub host;
        public string Title => "快速测试";
        public int SortOrder => -100;
        internal QuickTestPanel(QuickTestHub host) { this.host = host; }
        public Vector2 GetPreferredSize() => new Vector2(480, 280);
        public void Draw(RuntimeDebugPanelContext context)
        {
            GUILayout.Label("测试项目（记住上次选择）", context.LabelStyle);
            GUILayout.SelectionGrid(0, new[] { "已着陆 · 矿镐" }, 1, context.ButtonStyle);
            GUILayout.Label("正式星球地图，飞船已安全着陆。\n主角在舱外并装备矿镐，每次启动是干净的新局。", context.LabelStyle);
            GUILayout.Space(12);
            bool before = GUI.enabled;
            try
            {
                GUI.enabled = before && host != null && host.Available;
                if (GUILayout.Button("启动所选测试", context.ButtonStyle, GUILayout.Height(34)) && host.RequestLaunch())
                    RuntimeDebugHub.Toggle();
            }
            finally { GUI.enabled = before; }
        }
    }
}
#endif
