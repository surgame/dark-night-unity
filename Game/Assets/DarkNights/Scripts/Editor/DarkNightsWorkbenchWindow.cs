using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 三个任务工作区的宿主；只有内嵌编辑保留配置目录，场景和专用工具直接呈现操作。
    /// 序列化仅保存导航与选择；重建和关闭释放句柄，切区不保存资产或应用专用工具草稿。
    /// </summary>
    internal sealed class DarkNightsWorkbenchWindow : EditorWindow
    {
        internal const string MenuPath = "Dark Nights/工作台";
        private const string Layout = "Assets/DarkNights/Res/Editor/Workbench/DarkNightsWorkbench.uxml";
        [SerializeField] private string selectedId = "mining";
        [SerializeField] private string workspace = "editors";
        [SerializeField] private string search = "", sceneSearch = "", toolSearch = "";
        [SerializeField] private string sceneGroup = "全部", toolGroup = "全部";
        [SerializeField] private MiningDefinitionPanelState miningSelection = new MiningDefinitionPanelState();
        [SerializeField] private DarkNightsDefinitionBrowserState browserSelection = new DarkNightsDefinitionBrowserState();
        [SerializeField] private string sourceEntry = "", sourcePath = "";
        private string drawnEntry = "";
        private ScrollView navigation, details;
        private VisualElement editorBody, launcherBody;
        private ToolbarSearchField searchField;
        private Label status;
        private UnityEditor.Editor inspector;
        private DarkNightsDefinitionEditor definitionEditor;
        private MiningDefinitionPanel miningPanel;
        private DarkNightsDefinitionBrowser definitions;
        private DarkNightsWorkbenchLauncher launcher;

        [MenuItem(MenuPath, false, 0)]
        public static void Open()
        {
            var window = GetWindow<DarkNightsWorkbenchWindow>("Dark Nights 工作台");
            window.minSize = new Vector2(800, 540); window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            ReleasePanels(); rootVisualElement.Clear();
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Layout);
            if (layout == null) { rootVisualElement.Add(new HelpBox("工作台布局未导入：" + Layout, HelpBoxMessageType.Error)); return; }
            layout.CloneTree(rootVisualElement);
            rootVisualElement.AddToClassList("dn-workbench");
            rootVisualElement.EnableInClassList("dn-light", !EditorGUIUtility.isProSkin);
            editorBody = rootVisualElement.Q("editor-body"); launcherBody = rootVisualElement.Q("launcher-body");
            navigation = rootVisualElement.Q<ScrollView>("editor-navigation");
            details = rootVisualElement.Q<ScrollView>("editor-details");
            status = rootVisualElement.Q<Label>("workbench-status");
            searchField = rootVisualElement.Q<ToolbarSearchField>("workbench-search");
            searchField.RegisterValueChangedCallback(change =>
            {
                if (workspace == "editors") { search = change.newValue; RebuildNavigation(); }
                else
                {
                    if (workspace == "scenes") sceneSearch = change.newValue; else toolSearch = change.newValue;
                    launcher?.Filter(change.newValue);
                }
            });
            foreach (string id in new[] { "editors", "scenes", "tools" })
                rootVisualElement.Q<Button>("workspace-" + id).clicked += () => ShowWorkspace(id);
            rootVisualElement.Q<Button>("clear-search").clicked += () => searchField.value = "";
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            var legacy = DarkNightsWorkbenchCatalog.Entries.FirstOrDefault(entry => entry.Id == selectedId);
            if (legacy != null && legacy.Kind != DarkNightsWorkbenchEntryKind.Editor)
            { workspace = legacy.Kind == DarkNightsWorkbenchEntryKind.Tool ? "tools" : "scenes"; selectedId = "mining"; }
            ShowWorkspace(workspace);
        }

        private void OnGeometryChanged(GeometryChangedEvent change) =>
            rootVisualElement.EnableInClassList("dn-compact", change.newRect.width < 1050);

        private void ShowWorkspace(string id)
        {
            workspace = id == "scenes" || id == "tools" ? id : "editors";
            bool editing = workspace == "editors";
            editorBody.style.display = editing ? DisplayStyle.Flex : DisplayStyle.None;
            launcherBody.style.display = editing ? DisplayStyle.None : DisplayStyle.Flex;
            foreach (string tab in new[] { "editors", "scenes", "tools" })
                rootVisualElement.Q<Button>("workspace-" + tab).EnableInClassList("dn-tab-active", tab == workspace);
            searchField.SetValueWithoutNotify(editing ? search : workspace == "scenes" ? sceneSearch : toolSearch);
            searchField.tooltip = editing ? "搜索配置名称、用途和资产路径" : "搜索当前工作区的名称、用途和路径";
            launcher?.Dispose(); launcher = null; launcherBody.Clear();
            if (editing) { RebuildNavigation(); return; }
            ClearInspector(); drawnEntry = ""; details.Clear();
            miningPanel?.Dispose(); miningPanel = null; definitions?.Dispose(); definitions = null;
            bool scenes = workspace == "scenes";
            launcher = new DarkNightsWorkbenchLauncher(scenes, scenes ? sceneSearch : toolSearch, scenes ? sceneGroup : toolGroup,
                value => { if (scenes) sceneGroup = value; else toolGroup = value; }, value => status.text = value);
            launcherBody.Add(launcher);
        }

        private void RebuildNavigation()
        {
            if (navigation == null || workspace != "editors") return;
            navigation.Clear();
            var entries = DarkNightsWorkbenchCatalog.Entries.Where(entry => entry.Kind == DarkNightsWorkbenchEntryKind.Editor && entry.Matches(search)).ToArray();
            foreach (var entry in entries)
            {
                var button = new Button(() => { selectedId = entry.Id; RebuildNavigation(); }) { name = "editor-" + entry.Id, tooltip = entry.Description };
                button.AddToClassList("dn-editor-link");
                button.Add(new Label(entry.Title) { pickingMode = PickingMode.Ignore });
                var group = new Label(entry.Group) { pickingMode = PickingMode.Ignore }; group.AddToClassList("dn-muted"); button.Add(group);
                navigation.Add(button);
            }
            status.text = entries.Length + " / 5 项配置 · 编辑原始资产，分别保存";
            if (entries.Length == 0)
            {
                ClearInspector(); drawnEntry = ""; details.Clear();
                details.Add(new HelpBox("没有匹配的配置。清除搜索可返回原先选择。", HelpBoxMessageType.Info)); return;
            }
            // 搜索不覆盖上次选择；当前项可见时复用整棵编辑树。
            var selected = entries.FirstOrDefault(entry => entry.Id == selectedId) ?? entries[0];
            foreach (var button in navigation.Query<Button>().ToList())
                button.EnableInClassList("dn-editor-link-active", button.name == "editor-" + selected.Id);
            if (drawnEntry != selected.Id) DrawDetails(selected);
        }

        private void DrawDetails(DarkNightsWorkbenchEntry entry)
        {
            ClearInspector(); drawnEntry = entry.Id; details.Clear();
            var title = new Label(entry.Title); title.AddToClassList("dn-page-title"); details.Add(title);
            var description = new Label(entry.Description); description.AddToClassList("dn-description"); details.Add(description);
            var hint = new Label(entry.SaveHint); hint.AddToClassList("dn-save-hint"); details.Add(hint);
            if (entry.Id == "mining")
            {
                if (miningPanel == null) miningPanel = new MiningDefinitionPanel(miningSelection ??= new MiningDefinitionPanelState());
                details.Add(miningPanel);
                var open = new Button(MiningDefinitionWindow.Open) { text = "在独立窗口继续编辑" }; open.AddToClassList("dn-secondary"); details.Add(open); return;
            }
            if (entry.Id == "objects")
            {
                if (definitions == null) definitions = new DarkNightsDefinitionBrowser(browserSelection ??= new DarkNightsDefinitionBrowserState());
                details.Add(definitions); return;
            }
            var paths = entry.Assets.ToList();
            var choice = new PopupField<string>("作者资产", paths, sourceEntry == entry.Id ? Math.Max(0, paths.IndexOf(sourcePath)) : 0);
            choice.name = "source-choice"; details.Add(choice);
            var assetArea = new VisualElement(); assetArea.AddToClassList("dn-asset-area"); details.Add(assetArea);
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
            var actions = new VisualElement(); actions.AddToClassList("dn-actions"); area.Add(actions);
            actions.Add(new Button(() => Locate(asset)) { text = "在 Project 中定位" });
            if (asset is TextAsset || path.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".uss", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tss", StringComparison.OrdinalIgnoreCase))
            { actions.Add(new Button(() => AssetDatabase.OpenAsset(asset)) { text = "用原生 / 外部编辑器打开" }); return; }
            if (asset is GameCore.Objects.Definition.ObjectDefinition definition)
            { definitionEditor = new DarkNightsDefinitionEditor(definition); area.Add(definitionEditor); return; }
            UnityEditor.Editor.CreateCachedEditor(asset, null, ref inspector);
            var state = new Label(); state.AddToClassList("dn-save-hint"); area.Add(state);
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
        private void ReleasePanels()
        {
            ClearInspector(); miningPanel?.Dispose(); miningPanel = null; definitions?.Dispose(); definitions = null;
            launcher?.Dispose(); launcher = null; drawnEntry = "";
        }
        private void OnDisable()
        {
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged); ReleasePanels();
        }
    }
}
