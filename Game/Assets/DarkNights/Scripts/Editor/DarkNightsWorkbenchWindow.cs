using System;
using System.Linq;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor
{
    /// <summary>
    /// 项目任务与原生编辑器的聚合操作栏；四类任务只提供已有工具和常用来源，不拥有 Definition 编辑树。
    /// 序列化导航与校验输入，切任务释放辅助区；关闭工作台不会关闭编辑器或提交其草稿。
    /// </summary>
    internal sealed class DarkNightsWorkbenchWindow : EditorWindow
    {
        internal const string MenuPath = "Dark Nights/工作台";
        private const string Layout = "Assets/DarkNights/Res/Editor/Workbench/DarkNightsWorkbench.uxml";
        private static readonly string[] Tasks = { "objects", "journey", "map", "ui" };
        private static readonly string[] Groups = { DarkNightsWorkbenchCatalog.Objects, DarkNightsWorkbenchCatalog.Journey,
            DarkNightsWorkbenchCatalog.Map, DarkNightsWorkbenchCatalog.UI };
        [SerializeField] private string workspace = "objects";
        [SerializeField] private string sceneSearch = "", sceneGroup = "全部", sourcePath = "", rulesSourcePath = "";
        [SerializeField] private bool scenesExpanded;
        [SerializeField] private MiningDefinitionPanelState miningSelection = new MiningDefinitionPanelState();
        private ScrollView content;
        private Label status;
        private HelpBox error;
        private Foldout scenes;
        private DarkNightsWorkbenchLauncher launcher;
        private MiningDefinitionPanel miningPanel;
        private DarkNightsWorkbenchSources sources;
        private IVisualElementScheduledItem refresh;

        [MenuItem(MenuPath, false, 0)]
        public static void Open()
        {
            var window = GetWindow<DarkNightsWorkbenchWindow>("Dark Nights 工作台", true, typeof(SceneView));
            window.minSize = new Vector2(640, 460); window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            ReleasePanels(); refresh?.Pause(); rootVisualElement.Clear();
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Layout);
            if (layout == null) { rootVisualElement.Add(new HelpBox("工作台布局未导入：" + Layout, HelpBoxMessageType.Error)); return; }
            layout.CloneTree(rootVisualElement);
            rootVisualElement.AddToClassList("dn-workbench");
            rootVisualElement.EnableInClassList("dn-light", !EditorGUIUtility.isProSkin);
            content = rootVisualElement.Q<ScrollView>("task-content");
            status = rootVisualElement.Q<Label>("workbench-status");
            error = rootVisualElement.Q<HelpBox>("workbench-error"); error.style.display = DisplayStyle.None;
            foreach (string id in Tasks) rootVisualElement.Q<Button>("workspace-" + id).clicked += () => ShowWorkspace(id);
            BuildSceneShortcuts();
            scenes = rootVisualElement.Q<Foldout>("all-scenes");
            scenes.SetValueWithoutNotify(scenesExpanded);
            scenes.RegisterValueChangedCallback(change =>
            {
                if (change.target != scenes) return;
                scenesExpanded = change.newValue; ShowScenes();
            });
            refresh = rootVisualElement.schedule.Execute(UpdateAvailability).Every(250);
            ShowWorkspace(workspace); ShowScenes();
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void OnGeometryChanged(GeometryChangedEvent change) =>
            rootVisualElement.EnableInClassList("dn-compact", change.newRect.width < 950);

        private void OnEnable() => EditorApplication.projectChanged += OnProjectChanged;
        private void OnProjectChanged() { if (content != null) CreateGUI(); }

        private void BuildSceneShortcuts()
        {
            var row = rootVisualElement.Q("scene-shortcuts");
            foreach (string id in new[] { "bootstrap", "expedition", "random", "reference" })
            {
                var entry = GameSceneWorkbenchCatalog.Entries.Single(item => item.Id == id);
                var action = new Button(() => Execute(entry)) { text = entry.Title, name = "quick-" + id, tooltip = entry.Description };
                action.userData = AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.Assets[0]) != null;
                action.AddToClassList("dn-scene-button"); row.Add(action);
            }
        }

        private void ShowScenes()
        {
            launcher?.Dispose(); launcher = null; scenes.contentContainer.Clear();
            if (!scenesExpanded) return;
            var search = new ToolbarSearchField { name = "scene-search", value = sceneSearch };
            scenes.Add(search);
            launcher = new DarkNightsWorkbenchLauncher(sceneSearch, sceneGroup,
                value => sceneGroup = value, _ => { });
            scenes.Add(launcher);
            search.RegisterValueChangedCallback(change => { sceneSearch = change.newValue; launcher?.Filter(change.newValue); });
        }

        private void ShowWorkspace(string id)
        {
            int index = Array.IndexOf(Tasks, id); if (index < 0) index = 0;
            workspace = Tasks[index];
            ReleaseTask(); content.Clear();
            foreach (string task in Tasks) rootVisualElement.Q<Button>("workspace-" + task).EnableInClassList("dn-tab-active", task == workspace);
            var heading = new Label(Groups[index]); heading.AddToClassList("dn-page-title"); content.Add(heading);
            foreach (var entry in DarkNightsWorkbenchCatalog.Entries.Where(entry => entry.Group == Groups[index]))
            {
                if (entry.Kind == DarkNightsWorkbenchEntryKind.Tool) content.Add(ToolRow(entry));
                else if (entry.Kind == DarkNightsWorkbenchEntryKind.Editor)
                {
                    sources = new DarkNightsWorkbenchSources(entry, entry.Id == "ui" ? sourcePath : rulesSourcePath,
                        value => { if (entry.Id == "ui") sourcePath = value; else rulesSourcePath = value; });
                    content.Add(sources);
                }
            }
            if (workspace == "objects")
            {
                content.Add(new HelpBox("配置编辑与保存由原生 Workshop 管理；缺少配置时请显式同步或保存，采集辅助区只做校验。", HelpBoxMessageType.Info));
                AddDefinitionShortcuts();
                var validation = new Foldout { text = "采集装配与目标匹配", value = true, name = "mining-validation" };
                miningPanel = new MiningDefinitionPanel(miningSelection ??= new MiningDefinitionPanelState());
                validation.Add(miningPanel); content.Add(validation);
            }
            else if (workspace == "journey") AddDefinition("会话能力", DarkNightsWorkbenchCatalog.Session);
            UpdateAvailability();
        }

        private VisualElement ToolRow(DarkNightsWorkbenchEntry entry)
        {
            var row = new VisualElement(); row.AddToClassList("dn-tool-row");
            var info = new VisualElement(); info.AddToClassList("dn-entry-info"); row.Add(info);
            var title = new Label(entry.Title); title.AddToClassList("dn-entry-title"); info.Add(title);
            var description = new Label(entry.Description); description.AddToClassList("dn-description"); info.Add(description);
            var action = new Button(() => Execute(entry)) { text = entry.ActionLabel, name = "action-" + entry.Id, tooltip = entry.SaveHint };
            action.AddToClassList("dn-native-action"); row.Add(action); return row;
        }

        private void AddDefinitionShortcuts()
        {
            var row = new VisualElement(); row.AddToClassList("dn-definition-shortcuts"); content.Add(row);
            AddDefinition("矿镐", DarkNightsWorkbenchCatalog.ShipTrade + "item-pickaxe.asset", row);
            AddDefinition("矿床", DarkNightsWorkbenchCatalog.Root + "Objects/MineralDeposit/MineralDeposit.asset", row);
            AddDefinition("手枪", DarkNightsWorkbenchCatalog.ShipTrade + "item-pistol.asset", row);
            AddDefinition("炸药", DarkNightsWorkbenchCatalog.ShipTrade + "item-bomb.asset", row);
            AddDefinition("喷气背包", DarkNightsWorkbenchCatalog.ShipTrade + "item-jetpack.asset", row);
            AddDefinition("商店", DarkNightsWorkbenchCatalog.ShipTrade + "ship-service-shop.asset", row);
            AddDefinition("出售服务", DarkNightsWorkbenchCatalog.ShipTrade + "ship-service-sale.asset", row);
            AddDefinition("会话能力", DarkNightsWorkbenchCatalog.Session, row);
        }

        private void AddDefinition(string title, string path, VisualElement row = null)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(path);
            var button = new Button(() =>
            {
                if (definition == null || DarkNightsNativeWorkspace.Blocked) return;
                try { DarkNightsNativeWorkspace.Workshop(definition); }
                catch (Exception exception) { ShowError(exception); }
            }) { text = title, tooltip = path, name = "definition-" + System.IO.Path.GetFileNameWithoutExtension(path) };
            button.AddToClassList("dn-native-action"); button.EnableInClassList("dn-missing", definition == null);
            (row ?? content).Add(button);
        }

        private void Execute(DarkNightsWorkbenchEntry entry)
        {
            if (entry.Kind != DarkNightsWorkbenchEntryKind.Reference && DarkNightsNativeWorkspace.Blocked) return;
            try { entry.OpenEditor?.Invoke(); error.style.display = DisplayStyle.None; }
            catch (Exception exception) { ShowError(exception); }
        }

        private void ShowError(Exception exception) { error.text = exception.Message; error.style.display = DisplayStyle.Flex; }

        private void UpdateAvailability()
        {
            if (status == null) return;
            foreach (var button in rootVisualElement.Query<Button>(className: "dn-native-action").ToList())
                button.SetEnabled(!DarkNightsNativeWorkspace.Blocked && !button.ClassListContains("dn-missing"));
            string active = SceneManager.GetActiveScene().path;
            foreach (string id in new[] { "bootstrap", "expedition", "random", "reference" })
            {
                var entry = GameSceneWorkbenchCatalog.Entries.Single(item => item.Id == id);
                var button = rootVisualElement.Q<Button>("quick-" + id);
                button.SetEnabled(!DarkNightsNativeWorkspace.Blocked && button.userData is bool exists && exists);
                button.EnableInClassList("dn-current-scene", active == entry.Assets[0]);
            }
            status.text = DarkNightsNativeWorkspace.Blocked ? "Play／导入／编译期间暂停项目操作。" :
                "新编辑器优先同区停靠；已打开窗口原位复用。场景：" + SceneManager.GetActiveScene().name;
        }

        internal static void Locate(UnityEngine.Object asset)
        {
            if (asset == null) return;
            if (!(asset is ObjectDefinition)) Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
        private void ReleaseTask() { miningPanel?.Dispose(); miningPanel = null; sources?.Dispose(); sources = null; }
        private void ReleasePanels() { ReleaseTask(); launcher?.Dispose(); launcher = null; }
        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            refresh?.Pause(); ReleasePanels();
        }
    }
}
