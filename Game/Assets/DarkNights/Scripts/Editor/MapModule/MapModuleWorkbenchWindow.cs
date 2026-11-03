using System;
using System.Linq;
using DarkNights.Entry;
using DarkNights.Entry.MapModule;
using DarkNights.Runtime.Physics;
using DarkNights.View.MapModule;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DarkNights.Editor.MapModule
{
    /// <summary>
    /// 地图工坊的独立浏览和作者操作入口；地图选择仅更新窗口持有的只读原生预览。
    /// Scene／Prefab始终拥有作者数据，只有明确打开作者场景或调试才切换Unity场景。
    /// </summary>
    public sealed class MapModuleWorkbenchWindow : EditorWindow
    {
        private readonly MapModuleCanvas canvas = new MapModuleCanvas();
        private readonly MapModuleStudyNavigation navigation = new MapModuleStudyNavigation();
        private readonly MapModuleStudyPreview preview = new MapModuleStudyPreview();
        [SerializeField] private string selectedPath = "";
        [SerializeField] private string selectedCase = "";
        private MapModuleStudyAuthoring study;
        private MapModuleCaseAuthoring sample;
        private MapModuleAuthoring module;
        private HangingRopePlacement rope;
        private VisualElement studies, modules, objects, details;
        private Label status, version, context;
        private IMGUIContainer viewport;
        private string feedback = "从左侧选择地图，点击“切换场景”后可自行点击 Unity ▶ 运行。";
        private string checkedHash = "";
        private double nextUpdate;
        private bool rebuilding, binding;
        private bool AuthorLoaded => study != null && !EditorSceneManager.IsPreviewScene(study.gameObject.scene);

        public static void Open()
        {
            var window = GetWindow<MapModuleWorkbenchWindow>();
            window.titleContent = new GUIContent("地图工坊");
            window.minSize = new Vector2(1080, 660); window.Show();
        }
        public void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MapModulePaths.Ui);
            if (tree == null) { rootVisualElement.Add(new Label("地图工坊布局尚未导入。")); return; }
            rootVisualElement.Clear(); tree.CloneTree(rootVisualElement);
            studies = rootVisualElement.Q("studies"); objects = rootVisualElement.Q("objects");
            modules = rootVisualElement.Q("modules"); details = rootVisualElement.Q("details");
            status = rootVisualElement.Q<Label>("status"); version = rootVisualElement.Q<Label>("version");
            context = rootVisualElement.Q<Label>("context");
            Bind("initialize", InitializeStudy); Bind("open-author", () => OpenAuthor());
            Bind("locate-study", () => Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(selectedPath));
            Bind("save", Save); Bind("check", Check); Bind("prepare", Prepare); Bind("run", Run); Bind("fit", canvas.Fit);
            var filter = rootVisualElement.Q<TextField>("study-filter");
            filter.value = navigation.Filter;
            filter.RegisterValueChangedCallback(e => { navigation.Filter = e.newValue; DrawMaps(); });
            rootVisualElement.Q<Toggle>("bounds").RegisterValueChangedCallback(e => { canvas.Bounds = e.newValue; viewport.MarkDirtyRepaint(); });
            rootVisualElement.Q<Toggle>("ports").RegisterValueChangedCallback(e => { canvas.Ports = e.newValue; viewport.MarkDirtyRepaint(); });
            viewport = new IMGUIContainer(() => canvas.Draw(new Rect(Vector2.zero,
                rootVisualElement.Q("canvas").contentRect.size), sample, module, rope));
            viewport.AddToClassList("viewport"); rootVisualElement.Q("canvas").Add(viewport);
            canvas.Selected = SelectModule; canvas.RopeSelected = SelectRope;
            navigation.Refresh(); ResolveSelection(); Rebuild();
        }
        private void OnEnable()
        {
            EditorApplication.update += Tick; EditorApplication.hierarchyChanged += HierarchyChanged;
            Undo.undoRedoPerformed += HierarchyChanged; EditorApplication.projectChanged += ProjectChanged;
            EditorSceneManager.activeSceneChangedInEditMode += ActiveSceneChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
        }
        private void OnDisable()
        {
            EditorApplication.update -= Tick; EditorApplication.hierarchyChanged -= HierarchyChanged;
            Undo.undoRedoPerformed -= HierarchyChanged; EditorApplication.projectChanged -= ProjectChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= ActiveSceneChanged;
            EditorApplication.playModeStateChanged -= PlayModeChanged; preview.Dispose();
        }
        private void Bind(string name, Action action) => rootVisualElement.Q<Button>(name).clicked += () => Execute(action);
        private void Execute(Action action)
        {
            try { action(); } catch (Exception error) { feedback = error.Message; }
            RefreshStatus();
        }
        private void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextUpdate || rootVisualElement.childCount == 0) return;
            nextUpdate = EditorApplication.timeSinceStartup + .4;
            RefreshStatus(); viewport?.MarkDirtyRepaint();
        }
        private void HierarchyChanged()
        {
            if (rebuilding || binding || studies == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Execute(() => { ResolveSelection(); Rebuild(); });
        }
        private void ProjectChanged()
        {
            if (binding || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Execute(() => { navigation.Refresh(); preview.Reload(); ResolveSelection(); Rebuild(); });
        }
        private void ActiveSceneChanged(Scene before, Scene after) => HierarchyChanged();
        private void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) { preview.Dispose(); study = null; sample = null; }
            if (state == PlayModeStateChange.EnteredEditMode) HierarchyChanged();
        }
        private void DrawMaps() => navigation.Draw(studies, selectedPath, selectedCase,
            (path, id) => Execute(() => SelectMap(path, id)));
        private void SelectMap(string path, string id)
        {
            selectedPath = path; selectedCase = id; module = null; rope = null; checkedHash = "";
            ResolveSelection(); canvas.Fit(); feedback = "正在预览所选地图。点击“切换场景”即可打开并编辑。"; Rebuild();
        }
        private void ResolveSelection()
        {
            if (binding || EditorApplication.isPlayingOrWillChangePlaymode) return;
            binding = true;
            try
            {
                if (string.IsNullOrEmpty(selectedPath))
                {
                    var active = MapModuleStudyPreview.Find(SceneManager.GetActiveScene());
                    selectedPath = active != null ? active.gameObject.scene.path : navigation.First.Path;
                    selectedCase = active != null ? active.Cases.FirstOrDefault()?.CaseId ?? "" : navigation.First.CaseId;
                }
                if (string.IsNullOrEmpty(selectedPath)) { study = null; sample = null; return; }
                study = MapModuleStudyPreview.Find(preview.Select(selectedPath));
                if (study == null) throw new InvalidOperationException("场景中缺少地图配置，请检查 MapModuleStudyAuthoring 组件。");
                sample = string.IsNullOrEmpty(selectedCase) ? study.Cases.FirstOrDefault() : study.RequiredCase(selectedCase);
                selectedCase = sample != null ? sample.CaseId : "";
                if (sample != null && (module == null || !sample.Modules.Contains(module))) module = sample.Modules.FirstOrDefault();
                if (rope != null && !Ropes().Contains(rope)) rope = null;
            }
            finally { binding = false; }
        }
        private void Rebuild()
        {
            if (studies == null) return;
            rebuilding = true;
            try
            {
                modules.Clear(); objects.Clear(); DrawMaps();
                context.text = sample == null ? "选择地图" : sample.DisplayName;
                foreach (var item in sample != null ? sample.Modules.Where(m => m != null) : Enumerable.Empty<MapModuleAuthoring>())
                {
                    var button = new Button(() => SelectModule(item)) { text = item.DisplayName + "  " + item.Footprint.x + "×" + item.Footprint.y };
                    button.EnableInClassList("selected", item == module && rope == null); modules.Add(button);
                }
                foreach (var item in Ropes())
                {
                    var button = new Button(() => SelectRope(item)) { text = "吊绳 " + item.RopeId + " · " + item.SegmentCount + "节" };
                    button.EnableInClassList("selected", item == rope); objects.Add(button);
                }
                rootVisualElement.Q("objects-section").EnableInClassList("hidden", objects.childCount == 0);
                MapModuleDetailsPanel.Draw(details, sample, module, rope, AuthorLoaded, Execute);
            }
            finally { rebuilding = false; RefreshStatus(); }
        }
        private HangingRopePlacement[] Ropes() => sample == null ? Array.Empty<HangingRopePlacement>() :
            sample.Modules.Where(m => m != null).SelectMany(m => m.GetComponentsInChildren<HangingRopePlacement>(true)).Distinct().ToArray();
        private void SelectModule(MapModuleAuthoring value) { module = value; rope = null; Rebuild(); }
        private void SelectRope(HangingRopePlacement value) { rope = value; Rebuild(); }
        private bool OpenAuthor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("请先停止运行、等待编译完成，并退出Prefab 编辑模式。");
            if (string.IsNullOrEmpty(selectedPath)) return false;
            binding = true;
            try
            {
                var loaded = SceneManager.GetSceneByPath(selectedPath);
                if (loaded.IsValid() && loaded.isLoaded && !EditorSceneManager.IsPreviewScene(loaded)) SceneManager.SetActiveScene(loaded);
                else
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
                    preview.Dispose(); EditorSceneManager.OpenScene(selectedPath, OpenSceneMode.Single);
                }
            }
            finally { binding = false; }
            ResolveSelection();
            SessionState.SetString(MapModuleEntry.SelectionKey, selectedCase);
            SessionState.SetString(MapModuleEntry.StudyKey, study.StudyId);
            SessionState.EraseString(MapModuleEntry.VersionKey);
            checkedHash = ""; feedback = "场景已打开，可点击 Unity ▶ 运行。修改布局后，请先保存场景并生成运行地图。";
            Rebuild(); return true;
        }
        private void InitializeStudy()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            MapModuleStudySetup.Create(); selectedPath = MapModulePaths.Scene; selectedCase = "";
            navigation.Refresh(); ResolveSelection(); canvas.Fit(); Rebuild();
        }
        private void Save()
        {
            RequireStudy(true);
            if (!EditorSceneManager.SaveScene(study.gameObject.scene)) throw new InvalidOperationException("场景保存失败。");
            feedback = "场景已保存。模块轮廓的修改请在Prefab 编辑模式中保存。";
        }
        private void Check()
        {
            RequireStudy(); MapModuleCompiler.Check(study, sample); checkedHash = MapModuleSource.Hash(study, sample);
            feedback = "地形形状、模块连接和出生空间检查通过。移动和联机效果需要实际试玩确认。";
        }
        private void Prepare()
        {
            RequireStudy(true); var bake = MapModuleCompiler.Prepare(study, sample);
            checkedHash = bake.SourceHash; feedback = "运行地图已生成，场景已保存。版本 " + bake.ContentHash.Substring(0, 12);
        }
        private void Run()
        {
            if (!OpenAuthor()) return;
            RequireStudy(true);
            if (study.gameObject.scene.isDirty) throw new InvalidOperationException("请先保存场景，再启动调试。");
            Prepare(); SceneManager.SetActiveScene(study.gameObject.scene);
            SessionState.SetString(MapModuleEntry.SelectionKey, sample.CaseId);
            SessionState.SetString(MapModuleEntry.VersionKey, sample.Bake.ContentHash);
            SessionState.SetString(MapModuleEntry.StudyKey, study.StudyId);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePaths.Bootstrap);
            EditorApplication.isPlaying = true;
        }
        private void RequireStudy(bool author = false)
        {
            if (study == null || sample == null) throw new InvalidOperationException("先从左侧选择地图。");
            if (author && !AuthorLoaded) throw new InvalidOperationException("请先点击“切换场景”打开所选地图，再编辑或保存。");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先停止运行，并退出Prefab 编辑模式，再编辑地图。");
        }
        private void RefreshStatus()
        {
            if (status == null) return;
            status.text = EditorApplication.isPlayingOrWillChangePlaymode
                ? "正在运行；先点击 Unity ■ 停止，再选择地图并点击“切换场景”，随后可自行点击 Unity ▶ 运行。"
                : feedback;
            bool idle = !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
            bool selected = study != null && sample != null;
            try
            {
                string hash = selected ? MapModuleSource.Hash(study, sample) : "";
                bool fresh = selected && sample.Bake != null && sample.Bake.SourceHash == hash;
                version.text = !selected ? "未选择地图" : (AuthorLoaded ? "场景已打开 · " : "地图预览 · ") +
                    (AuthorLoaded && sample.gameObject.scene.isDirty ? "未保存 · " : "") +
                    (fresh ? "运行地图 " + sample.Bake.ContentHash.Substring(0, 12) : "请生成运行地图") +
                    (hash == checkedHash ? " · 本次检查通过" : "");
            }
            catch (Exception error) { version.text = error.Message; }
            foreach (string name in new[] { "save", "prepare" }) rootVisualElement.Q<Button>(name)?.SetEnabled(idle && selected && AuthorLoaded);
            foreach (string name in new[] { "check", "open-author", "run", "locate-study" }) rootVisualElement.Q<Button>(name)?.SetEnabled(idle && selected);
            rootVisualElement.Q<Button>("initialize")?.SetEnabled(idle && AssetDatabase.LoadAssetAtPath<SceneAsset>(MapModulePaths.Scene) == null);
        }
    }
}
