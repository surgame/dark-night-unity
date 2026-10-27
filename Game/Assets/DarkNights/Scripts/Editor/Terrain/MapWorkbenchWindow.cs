using System;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Scripting.APIUpdating;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 全游戏地图设计工作台的UITK宿主；作者草稿、样式草稿及Undo沿用原所有者，界面只组合制作能力。
    /// 沿原脚本GUID迁移窗口类型，域重载保留作者输入；关闭时释放预览、绑定与订阅，不隐式保存或装配。
    /// </summary>
    [MovedFrom(true, "DarkNights.Editor.Terrain", "DarkNights.Editor", "TerrainStylePreviewWindow")]
    public sealed class MapWorkbenchWindow : EditorWindow
    {
        [SerializeField] private ExpeditionFlowDraft flowDraft;
        [SerializeField] private TerrainStyleDrafts drafts = new TerrainStyleDrafts();
        [SerializeField] private CaveTerrainStyle style;
        [SerializeField] private ScriptableObject inspected;
        [SerializeField] private string planetId;
        [SerializeField] private MapWorkbenchState state = new MapWorkbenchState();
        private TerrainGenerationPreview generation;
        private MapWorkbenchPreview preview;
        private MapWorkbenchView view;
        private MapWorkbenchCommands commands;
        private MapWorkbenchFileLoad fileLoad;
        private readonly MapWorkbenchProgress progress = new MapWorkbenchProgress();
        private double nextRefresh;
        private double generateAfter = double.PositiveInfinity;
        private string observedStyle, observedSource;
        private bool wasPlaying;
        internal bool WaitingForInput => !double.IsPositiveInfinity(generateAfter) &&
            EditorApplication.timeSinceStartup < generateAfter;
        public ExpeditionFlowDraft Draft => flowDraft;
        internal TerrainGenerationPreview Generation => generation;
        internal TerrainStyleDrafts Styles => drafts;
        internal CaveTerrainStyle Style => style;
        internal ScriptableObject Inspected { get => inspected; set => inspected = value; }
        internal MapWorkbenchState State => state;
        internal MapWorkbenchPreview Preview => preview;
        internal MapWorkbenchCommands Commands => commands;
        internal MapWorkbenchFileLoad FileLoad => fileLoad;
        internal MapWorkbenchProgress Progress => progress;
        internal string Notice { get; set; } = "编辑当前配置，调整后自动预览。";

        public static void Open() => DarkNightsNativeWorkspace.Visual();
        public static void OpenJourney()
        {
            Open();
            var window = GetWindow<MapWorkbenchWindow>();
            window.state.AdvancedPage = "planet"; window.SelectPage("advanced");
        }
        public static void OpenVeinStudy()
        {
            Open();
            var window = GetWindow<MapWorkbenchWindow>();
            window.state.ShowStudy = true; window.state.AdvancedPage = "study";
            window.SelectPage("advanced");
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("地图设计工作台"); minSize = new Vector2(1080, 720);
            state = state ?? new MapWorkbenchState();
            if (state.Page == "space" || state.Page == "source" || state.Page == "flow") state.Page = "recipe";
            if (state.Document != null)
                state.DocumentBaseline = TerrainGenerationMetadata.UpgradeBaseline(state.DocumentBaseline,
                    EditorJsonUtility.ToJson(state.Document));
            generation = new TerrainGenerationPreview(flowDraft); flowDraft = generation.Draft;
            generation.CaptureStages = true;
            flowDraft.KeepIndependent();
            if (!string.IsNullOrEmpty(planetId)) generation.PlanetId = planetId;

            if (style == null) style = MapWorkbenchFiles.DefaultStyle();
            if (inspected == null) inspected = style;
            commands = new MapWorkbenchCommands(this);
            preview = new MapWorkbenchPreview(this);
            fileLoad = new MapWorkbenchFileLoad(this);
            generation.RequestGeneration();
            if (state.ViewedMap != null) fileLoad.ViewFrozen(state.ViewedMap);
            else preview.RequestOpen();
            saveChangesMessage = "当前配置尚未保存。保存更新当前文件；另存为可创建副本。";
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.update += Tick;
        }

        public void CreateGUI()
        {
            view?.Dispose(); rootVisualElement.Clear();
            if (generation == null) return;
            view = new MapWorkbenchView(this, rootVisualElement);
            view.Build();
        }

        private void OnDisable()
        {
            planetId = generation?.PlanetId;
            EditorApplication.update -= Tick; Undo.undoRedoPerformed -= OnUndo;
            view?.Dispose(); view = null;
            fileLoad?.Dispose(); fileLoad = null;
            preview?.Dispose(); preview = null;
            generation?.Dispose(false); generation = null;
        }

        private void OnDestroy()
        {
            drafts.Dispose();
            if (flowDraft != null) { Undo.ClearUndo(flowDraft); DestroyImmediate(flowDraft); }
        }

        private void OnLostFocus() => preview?.StopStroke();

        internal void Execute(Action action, bool rebuildPage = false)
        {
            try { action(); }
            catch (Exception error) { Notice = error.Message; }
            if (rebuildPage) view?.BuildPage();
            Refresh();
        }

        internal void SelectPage(string page)
        {
            if (page == "space" || page == "source" || page == "flow") page = "recipe";
            if (page == "advanced" || page == "extras" || page == "minerals") state.ProductionStage = 7;
            state.Page = page;
            if (page != "extras") state.Pick = 0;
            if (page != "advanced") { state.ShowStudy = false; state.ShowJourney = false; preview?.UsePan(); }
            view?.BuildPage(); Refresh();
        }

        internal void DraftChanged()
        {
            if (fileLoad?.Busy == true) fileLoad.Cancel();
            state.CheckedIdentity = "";
            generateAfter = Math.Min(generateAfter, EditorApplication.timeSinceStartup + .08);
            Notice = "参数已变化，正在自动预览；保留镜头和上一次有效画面。";
            Refresh();
        }

        internal void RebuildPage() => view?.BuildPage();

        internal void SetDocumentStyle(CaveTerrainStyle value)
        {
            drafts.Clear(); style = value; inspected = value; observedStyle = null;
        }

        internal void Refresh()
        {
            if (generation == null) return;
            hasUnsavedChanges = generation.HasChanges || generation.CandidateNeedsSave ||
                drafts.HasChanges || TerrainVeinStudySettings.instance.HasChanges ||
                state.Document != null && generation.PlanetId != state.Document.PlanetId;
            view?.Refresh();
        }

        private void OnUndo()
        {
            fileLoad?.Cancel();
            if (state.Document != null) flowDraft.RestoreSavedBaseline(MapWorkbenchImport.Read(state.Document));
            state.CheckedIdentity = "";
            preview?.RestoreStyleAfterUndo();
            if (!generation.CandidateFresh) DraftChanged();
            Notice = "已撤销／重做，自动同步当前地图。";
            view?.BuildPage(); Refresh();
        }

        public void ImportLegacyDraft(ExpeditionFlowDraft legacy)
        {
            if (preview.Map.HasChanges) throw new InvalidOperationException("先取消临时拆填，再转移旧航程草稿。");
            generation.Import(legacy); view?.BuildPage(); DraftChanged();
        }

        public override void SaveChanges()
        {
            if (TerrainDocumentStore.Save(this) == null)
                throw new OperationCanceledException("已取消保存配置，未保存输入保留。");
            if (TerrainVeinStudySettings.instance.HasChanges) TerrainVeinStudySettings.instance.SavePreset();
            Refresh();
        }

        public override void DiscardChanges()
        {
            preview.Map.Cancel();
            if (state.Document != null) TerrainDocumentStore.Open(this, state.Document);
            else { generation.Cancel(); drafts.Clear(); flowDraft.KeepIndependent(); commands.Generate(); }
            TerrainVeinStudySettings.instance.DiscardPreset();
            state.CheckedIdentity = ""; view?.BuildPage();
            Notice = "已恢复配置上次保存的内容。"; Refresh();
        }

        private void Tick()
        {
            if (generation == null || preview == null) return;
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            if (playing) { if (!wasPlaying) { fileLoad.Cancel(); preview.Suspend(); } wasPlaying = true; Refresh(); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup >= generateAfter)
            {
                generateAfter = double.PositiveInfinity;
                if (generation.CandidateFresh)
                {
                    preview.RequestEnvironment();
                    Notice = "配置已更新；有效生成输入未变化，保留当前地图与矿区。";
                }
                else Execute(commands.Generate);
            }
            if (wasPlaying) { wasPlaying = false; preview.RequestOpen(); }
            try { fileLoad.Tick(); preview.Tick(); }
            catch (Exception error)
            { Notice = "预览失败：" + error.Message; preview.CancelPending(); progress.Finish("更新失败"); }
            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + .25;
            try
            {
                generation.ObserveExternalChanges();
                string source = generation.Draft.BaselineJson;
                if (observedSource != null && observedSource != source)
                { view?.BuildPage(); }
                observedSource = source;
                string current = style == null ? "none" : style.VisualIdentity;
                if (observedStyle != null && observedStyle != current)
                {
                    if (drafts.HasChanges) Notice = "共享样式在窗口外变化；保存时会检查字段冲突。";
                    else { drafts.Clear(); preview.RequestStyle(); view?.BuildPage(); }
                }
                observedStyle = current;
            }
            catch (Exception error) { Notice = "来源核对失败：" + error.Message; }
            Refresh();
        }
    }
}
