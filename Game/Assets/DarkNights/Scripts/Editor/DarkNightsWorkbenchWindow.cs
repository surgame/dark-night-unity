using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 按游戏、设置和开发模块导航的主工作台；日常入口保持精简，复杂工具与原生作者来源延迟放入扩展。
    /// 本窗口只持有导航、只读摘要和短期Inspector句柄，不拥有地图草稿、业务配置或运行权威状态。
    /// </summary>
    internal sealed class DarkNightsWorkbenchWindow : EditorWindow
    {
        internal const string MenuPath = "Dark Nights/工作台";
        private const string Layout = "Assets/DarkNights/Res/Editor/Workbench/DarkNightsWorkbench.uxml";
        [SerializeField] private string workspace = "level";
        [SerializeField] private string toolSearch = "";
        private ScrollView content;
        private Label status;
        private HelpBox error;
        private DarkNightsWorkbenchPage page;
        private IVisualElementScheduledItem refresh;

        [MenuItem(MenuPath, false, 0)]
        public static void Open() =>
            DarkNightsNativeWorkspace.Open<DarkNightsWorkbenchWindow>("Dark Nights 工作台", new Vector2(680, 480), true);

        public void CreateGUI()
        {
            ReleasePage(); refresh?.Pause();
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            rootVisualElement.Clear();
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Layout);
            if (layout == null) { rootVisualElement.Add(new HelpBox("工作台布局未导入：" + Layout, HelpBoxMessageType.Error)); return; }
            layout.CloneTree(rootVisualElement);
            rootVisualElement.AddToClassList("dn-workbench");
            rootVisualElement.EnableInClassList("dn-light", !EditorGUIUtility.isProSkin);
            content = rootVisualElement.Q<ScrollView>("task-content");
            status = rootVisualElement.Q<Label>("workbench-status");
            error = rootVisualElement.Q<HelpBox>("workbench-error"); error.style.display = DisplayStyle.None;
            BuildNavigation();
            var search = rootVisualElement.Q<TextField>("workbench-search");
            search.SetValueWithoutNotify(toolSearch);
            search.RegisterValueChangedCallback(change => { toolSearch = change.newValue; ShowSearch(); });
            rootVisualElement.Q<Button>("clear-search").clicked += () => search.value = "";
            refresh = rootVisualElement.schedule.Execute(UpdateAvailability).Every(500);
            SafeRun(() => ShowWorkspace(workspace));
            if (!string.IsNullOrWhiteSpace(toolSearch)) ShowSearch();
            rootVisualElement.EnableInClassList("dn-compact", position.width < 900);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void BuildNavigation()
        {
            var navigation = rootVisualElement.Q<ScrollView>("workspace-navigation");
            foreach (var section in DarkNightsWorkbenchCatalog.Tasks.GroupBy(task => task.Section))
            {
                var heading = new Label(section.Key); heading.AddToClassList("dn-nav-section"); navigation.Add(heading);
                foreach (var task in section)
                {
                    string id = task.Id;
                    var button = new Button(() => Navigate(id)) { name = "workspace-" + id, tooltip = task.Description };
                    button.AddToClassList("dn-nav-item");
                    var title = new Label(task.Title); title.AddToClassList("dn-nav-title"); button.Add(title);
                    var hint = new Label(task.Hint); hint.AddToClassList("dn-nav-hint"); button.Add(hint);
                    navigation.Add(button);
                }
            }
        }

        private void Navigate(string id) => NavigateTo(id, "");
        private void NavigateTo(string id, string focus)
        {
            toolSearch = ""; rootVisualElement.Q<TextField>("workbench-search").SetValueWithoutNotify("");
            rootVisualElement.Q<Button>("clear-search").style.display = DisplayStyle.None;
            SafeRun(() => ShowWorkspace(id, focus));
        }

        private void ShowWorkspace(string id, string focus = "")
        {
            if (id == "journey" || id == "ui") id = "ship";
            else if (id == "scenes") id = "level";
            else if (id == "maintenance") id = "diagnostics";
            var task = DarkNightsWorkbenchCatalog.Tasks.FirstOrDefault(item => item.Id == id);
            if (task.Id == null) task = DarkNightsWorkbenchCatalog.Tasks[0];
            workspace = task.Id; ReleasePage(); content.Clear();
            foreach (var item in DarkNightsWorkbenchCatalog.Tasks)
                rootVisualElement.Q<Button>("workspace-" + item.Id).EnableInClassList("dn-nav-active", item.Id == workspace);
            rootVisualElement.Q<Label>("task-section").text = task.Section;
            rootVisualElement.Q<Label>("task-title").text = task.Title;
            rootVisualElement.Q<Label>("task-description").text = task.Description;
            page = new DarkNightsWorkbenchPage(SafeRun); content.Add(page);
            if (workspace == "diagnostics") DarkNightsWorkbenchDiagnostics.Build(page, Execute);
            else if (workspace == "input" || workspace == "graphics") DarkNightsWorkbenchSettings.Build(workspace, page);
            else DarkNightsWorkbenchGame.Build(workspace, page);
            if (workspace == "level")
                foreach (string scene in new[] { "bootstrap", "expedition" })
                    page.Entry(GameSceneWorkbenchCatalog.Entries.Single(entry => entry.Id == scene));
            var entries = DarkNightsWorkbenchCatalog.Entries.Where(entry => entry.Group == task.Title).ToArray();
            foreach (var entry in entries)
            {
                if (entry.Extended) page.Extend(extra => extra.Entry(entry));
                else page.Entry(entry);
            }
            page.Finish(entries.Any(entry => entry.Extended && entry.Id == focus) ||
                workspace == "level" && focus.Length != 0);
            UpdateAvailability();
        }

        private void ShowSearch()
        {
            rootVisualElement.Q<Button>("clear-search").style.display =
                string.IsNullOrWhiteSpace(toolSearch) ? DisplayStyle.None : DisplayStyle.Flex;
            if (string.IsNullOrWhiteSpace(toolSearch)) { SafeRun(() => ShowWorkspace(workspace)); return; }
            ReleasePage(); content.Clear();
            rootVisualElement.Q<Label>("task-section").text = "全部模块";
            rootVisualElement.Q<Label>("task-title").text = "搜索结果";
            var matches = DarkNightsWorkbenchCatalog.Entries.Concat(DarkNightsWorkbenchCommands.Entries)
                .Where(entry => entry.Matches(toolSearch)).ToArray();
            rootVisualElement.Q<Label>("task-description").text = matches.Length + "个匹配项 · 含扩展工具 · Esc返回";
            foreach (var entry in matches)
                content.Add(DarkNightsWorkbenchCards.Create(entry, () =>
                {
                    if (entry.Kind == DarkNightsWorkbenchEntryKind.Editor)
                        NavigateTo(DarkNightsWorkbenchCatalog.Module(entry), entry.Id);
                    else Execute(entry);
                }, entry.Kind == DarkNightsWorkbenchEntryKind.Editor ? "查看来源" : null));
            if (matches.Length == 0)
            {
                var empty = new Label("没有找到匹配项。可搜索业务名称、资产路径或旧菜单文字。");
                empty.AddToClassList("dn-empty"); content.Add(empty);
            }
            UpdateAvailability();
        }

        private void Execute(DarkNightsWorkbenchEntry entry)
        {
            if (DarkNightsNativeWorkspace.CanOpen(entry.Kind)) SafeRun(() => entry.OpenEditor?.Invoke());
        }

        private void SafeRun(Action action)
        {
            try { action(); if (error != null) error.style.display = DisplayStyle.None; }
            catch (Exception exception)
            {
                if (error == null) return;
                error.text = exception.Message; error.style.display = DisplayStyle.Flex;
            }
        }

        private void UpdateAvailability()
        {
            if (status == null) return;
            foreach (var button in rootVisualElement.Query<Button>(className: "dn-native-action").ToList())
            {
                var kind = button.userData is DarkNightsWorkbenchEntry entry ? entry.Kind :
                    button.userData is DarkNightsWorkbenchEntryKind value ? value : DarkNightsWorkbenchEntryKind.Tool;
                button.SetEnabled(DarkNightsNativeWorkspace.CanOpen(kind) && !button.ClassListContains("dn-missing"));
            }
            status.text = EditorApplication.isCompiling || EditorApplication.isUpdating ? "导入／编译中，暂停作者操作。" :
                EditorApplication.isPlayingOrWillChangePlaymode ? "Play中可查看诊断；作者编辑与场景切换暂停。" :
                "就绪 · " + SceneManager.GetActiveScene().name + " · 扩展按需展开，原窗口保留草稿";
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            var search = rootVisualElement.Q<TextField>("workbench-search");
            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.K) { search.Focus(); evt.StopPropagation(); }
            else if (evt.keyCode == KeyCode.Escape && toolSearch.Length > 0) { search.value = ""; search.Focus(); evt.StopPropagation(); }
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) =>
            rootVisualElement.EnableInClassList("dn-compact", evt.newRect.width < 900);
        private void OnEnable()
        {
            EditorApplication.projectChanged += OnProjectChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        private void OnProjectChanged() { if (content != null) CreateGUI(); }
        private void OnPlayModeChanged(PlayModeStateChange state) { if (content != null) CreateGUI(); }
        private void ReleasePage() { page?.Dispose(); page = null; }
        internal static void Locate(UnityEngine.Object asset)
        {
            if (asset == null) return;
            if (!(asset is GameCore.Objects.Definition.ObjectDefinition)) Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            refresh?.Pause(); ReleasePage();
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }
    }
}
