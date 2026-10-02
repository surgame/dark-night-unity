using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 正式配置与预览的统一导航工作台；资产页复用原生 Inspector，复杂流程交给现有专用编辑器。
    /// 窗口只持有导航与 Inspector 缓存，切页不应用草稿、不生成资产，也不启动游戏会话。
    /// </summary>
    internal sealed class DarkNightsWorkbenchWindow : EditorWindow
    {
        internal const string MenuPath = "Dark Nights/工作台";
        [SerializeField] private string selectedId = "mining";
        [SerializeField] private string search = "";
        [SerializeField] private string group = "全部";
        [SerializeField] private MiningDefinitionPanelState miningSelection = new MiningDefinitionPanelState();
        [SerializeField] private DarkNightsDefinitionBrowserState browserSelection = new DarkNightsDefinitionBrowserState();
        [SerializeField] private string sourceEntry = "", sourcePath = "";
        private string drawnEntry = "";
        private ScrollView navigation, details;
        private UnityEditor.Editor inspector;
        private DarkNightsDefinitionEditor definitionEditor;
        private MiningDefinitionPanel miningPanel;
        private DarkNightsDefinitionBrowser definitions;

        [MenuItem(MenuPath, false, 0)]
        public static void Open()
        {
            var window = GetWindow<DarkNightsWorkbenchWindow>("Dark Nights 工作台");
            window.minSize = new Vector2(780, 540); window.Show();
        }

        public void CreateGUI()
        {
            ClearInspector(); miningPanel?.Dispose(); miningPanel = null; definitions?.Dispose(); definitions = null; drawnEntry = "";
            rootVisualElement.Clear();
            var header = new Label("Dark Nights 工作台") { style = { fontSize = 18, marginLeft = 12, marginTop = 10, marginBottom = 8 } };
            rootVisualElement.Add(header);
            var searchField = new ToolbarSearchField { value = search };
            searchField.style.marginLeft = searchField.style.marginRight = 10;
            searchField.RegisterValueChangedCallback(change => { search = change.newValue; RebuildNavigation(); });
            rootVisualElement.Add(searchField);
            var groups = new System.Collections.Generic.List<string> { "全部" };
            groups.AddRange(DarkNightsWorkbenchCatalog.Entries.Select(entry => entry.Group).Distinct());
            if (!groups.Contains(group)) group = "全部";
            var category = new DropdownField("分类", groups, Math.Max(0, groups.IndexOf(group))) { name = "workbench-category" };
            category.style.marginLeft = category.style.marginRight = 10;
            category.RegisterValueChangedCallback(change => { group = change.newValue; RebuildNavigation(); });
            rootVisualElement.Add(category);
            var body = new TwoPaneSplitView(0, 240, TwoPaneSplitViewOrientation.Horizontal) { style = { flexGrow = 1, marginTop = 10 } };
            navigation = new ScrollView { style = { minWidth = 180, paddingLeft = 10, paddingRight = 10 } };
            details = new ScrollView { style = { flexGrow = 1, paddingLeft = 12, paddingRight = 12, paddingBottom = 12 } };
            body.Add(navigation); body.Add(details); rootVisualElement.Add(body);
            RebuildNavigation();
        }

        private void RebuildNavigation()
        {
            if (navigation == null) return;
            navigation.Clear();
            var entries = DarkNightsWorkbenchCatalog.Entries.Where(entry => entry.Matches(search) && (group == "全部" || entry.Group == group) &&
                (entry.Group != "已退役" || group == "已退役" || !string.IsNullOrWhiteSpace(search))).ToArray();
            if (entries.Length > 0 && !entries.Any(entry => entry.Id == selectedId)) selectedId = entries[0].Id;
            foreach (var group in entries.GroupBy(entry => entry.Group))
            {
                navigation.Add(new Label(group.Key) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10, marginBottom = 5 } });
                foreach (var entry in group)
                {
                    var button = new Button(() => { selectedId = entry.Id; RebuildNavigation(); }) { text = entry.Title };
                    button.style.unityTextAlign = TextAnchor.MiddleLeft;
                    button.style.whiteSpace = WhiteSpace.Normal; button.style.minHeight = 30;
                    if (entry.Id == selectedId) button.style.unityFontStyleAndWeight = FontStyle.Bold;
                    navigation.Add(button);
                }
            }
            if (entries.Length == 0)
            {
                ClearInspector(); drawnEntry = ""; details.Clear(); details.Add(new HelpBox("没有匹配的入口，请更换搜索词。", HelpBoxMessageType.Info)); return;
            }
            if (drawnEntry != selectedId) DrawDetails(entries.Single(entry => entry.Id == selectedId));
        }

        private void DrawDetails(DarkNightsWorkbenchEntry entry)
        {
            ClearInspector(); drawnEntry = entry.Id; details.Clear();
            details.Add(new Label(entry.Title) { style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 } });
            details.Add(new HelpBox(entry.Description, HelpBoxMessageType.Info));
            details.Add(new HelpBox(entry.SaveHint, HelpBoxMessageType.None));
            if (entry.Id == "mining")
            {
                if (miningPanel == null) miningPanel = new MiningDefinitionPanel(miningSelection ??= new MiningDefinitionPanelState());
                details.Add(miningPanel);
                details.Add(new Button(MiningDefinitionWindow.Open) { text = "在独立窗口继续编辑" }); return;
            }
            if (entry.Id == "objects")
            {
                if (definitions == null) definitions = new DarkNightsDefinitionBrowser(browserSelection ??= new DarkNightsDefinitionBrowserState());
                details.Add(definitions); return;
            }
            if (entry.OpenEditor != null)
            {
                var open = new Button(() => entry.OpenEditor()) { text = entry.ActionLabel };
                open.style.height = 32; details.Add(open);
                details.Add(new Label("作者资产（点击在 Project 中定位）") { style = { marginTop = 12 } });
                foreach (string path in entry.Assets)
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    var button = new Button(() => Locate(asset)) { text = System.IO.Path.GetFileName(path), tooltip = path };
                    button.SetEnabled(asset != null); details.Add(button);
                    if (asset == null) details.Add(new HelpBox("资产不存在：" + path, HelpBoxMessageType.Warning));
                }
                return;
            }
            var paths = entry.Assets.ToList();
            var choice = new PopupField<string>("作者资产", paths, sourceEntry == entry.Id ? Math.Max(0, paths.IndexOf(sourcePath)) : 0);
            details.Add(choice);
            var assetArea = new VisualElement(); details.Add(assetArea);
            choice.RegisterValueChangedCallback(change => { sourceEntry = entry.Id; sourcePath = change.newValue; DrawAsset(assetArea, change.newValue); });
            DrawAsset(assetArea, choice.value);
        }

        private void DrawAsset(VisualElement area, string path)
        {
            ClearInspector(); area.Clear();
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) { area.Add(new HelpBox("资产不存在：" + path, HelpBoxMessageType.Warning)); return; }
            var source = new ObjectField("原始资产") { value = asset, objectType = asset.GetType(), allowSceneObjects = false };
            source.SetEnabled(false); area.Add(source);
            area.Add(new Button(() => Locate(asset)) { text = "在 Project 中定位" });
            if (asset is TextAsset || path.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".uss", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tss", StringComparison.OrdinalIgnoreCase))
            {
                area.Add(new Button(() => AssetDatabase.OpenAsset(asset)) { text = "用原生 / 外部编辑器打开" }); return;
            }
            if (asset is GameCore.Objects.Definition.ObjectDefinition definition)
            {
                definitionEditor = new DarkNightsDefinitionEditor(definition); area.Add(definitionEditor); return;
            }
            UnityEditor.Editor.CreateCachedEditor(asset, null, ref inspector);
            var state = new Label(); area.Add(state);
            area.Add(new IMGUIContainer(() =>
            {
                if (inspector == null || inspector.target != asset) return;
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                {
                    inspector.OnInspectorGUI();
                    if (GUILayout.Button("保存当前资产")) AssetDatabase.SaveAssetIfDirty(asset);
                }
                state.text = EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ? "Play／编译期间禁止编辑与保存。"
                    : EditorUtility.IsDirty(asset) ? "当前资产有未保存修改。" : "当前资产已保存。";
            }));
        }

        internal static void Locate(UnityEngine.Object asset)
        {
            if (asset == null) return;
            if (!(asset is GameCore.Objects.Definition.ObjectDefinition)) Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
        private void ClearInspector()
        {
            if (inspector != null) DestroyImmediate(inspector); inspector = null;
            definitionEditor?.Dispose(); definitionEditor = null;
        }
        private void OnDisable()
        {
            ClearInspector(); miningPanel?.Dispose(); miningPanel = null;
            definitions?.Dispose(); definitions = null;
        }
    }
}
