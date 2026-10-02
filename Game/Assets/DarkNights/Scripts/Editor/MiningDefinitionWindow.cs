using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 采集面板的独立宿主；与聚合工作台复用同一 UI Toolkit 面板，不维护第二份编辑或匹配逻辑。
    /// 域重载重新装配面板，窗口关闭释放原生 Inspector 和只读状态刷新。
    /// </summary>
    internal sealed class MiningDefinitionWindow : EditorWindow
    {
        private MiningDefinitionPanel panel;
        [SerializeField] private MiningDefinitionPanelState selection = new MiningDefinitionPanelState();
        public static void Open()
        {
            var window = GetWindow<MiningDefinitionWindow>("工具 Definition");
            window.minSize = new Vector2(640, 560); window.Show();
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
