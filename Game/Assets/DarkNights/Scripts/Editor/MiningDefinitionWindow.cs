using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 采集校验的独立宿主；与项目辅助区复用只读匹配面板，配置编辑统一交给原生 Workshop。
    /// 域重载恢复目标和匹配输入，关闭释放只读状态刷新，不保存作者资产。
    /// </summary>
    internal sealed class MiningDefinitionWindow : EditorWindow
    {
        private MiningDefinitionPanel panel;
        [SerializeField] private MiningDefinitionPanelState selection = new MiningDefinitionPanelState();
        public static void Open()
        {
            DarkNightsNativeWorkspace.Mining();
        }
        public void CreateGUI()
        {
            panel?.Dispose(); rootVisualElement.Clear();
            var scroll = new UnityEngine.UIElements.ScrollView();
            panel = new MiningDefinitionPanel(selection ??= new MiningDefinitionPanelState()); scroll.Add(panel); rootVisualElement.Add(scroll);
        }
        private void OnDisable() { panel?.Dispose(); panel = null; }
    }
}
