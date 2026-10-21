using System;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 原Tuner的UITK任务式宿主；作者草稿、样式草稿及Undo沿用原所有者，界面只组合制作能力。
    /// 保留窗口与类型身份，域重载保留作者输入；关闭时释放预览、绑定与订阅，不隐式保存或装配。
    /// </summary>
    public sealed class TerrainStylePreviewWindow : EditorWindow
    {
        private const string Root = "Assets/DarkNights/Res/Terrain/StrataCave/";
        [SerializeField] private ExpeditionFlowDraft flowDraft;
        [SerializeField] private TerrainStyleDrafts drafts = new TerrainStyleDrafts();
        [SerializeField] private CaveTerrainStyle style;
        [SerializeField] private ScriptableObject inspected;
        [SerializeField] private string planetId;
        [SerializeField] private TerrainWorkbenchState state = new TerrainWorkbenchState();
        private TerrainGenerationPreview generation;
        private TerrainWorkbenchPreview preview;
        private TerrainWorkbenchView view;
        private TerrainWorkbenchCommands commands;
        private double nextRefresh;
        private string observedStyle, observedSource;
        private bool wasPlaying;
        public ExpeditionFlowDraft Draft => flowDraft;
        internal TerrainGenerationPreview Generation => generation;
        internal TerrainStyleDrafts Styles => drafts;
        internal CaveTerrainStyle Style => style;
        internal ScriptableObject Inspected { get => inspected; set => inspected = value; }
        internal TerrainWorkbenchState State => state;
        internal TerrainWorkbenchPreview Preview => preview;
        internal TerrainWorkbenchCommands Commands => commands;
        internal string Notice { get; set; } = "选择地图来源，或查看正式新局引用；不会自动更换地图。";

        public static void Open() => DarkNightsNativeWorkspace.Visual();
        public static void OpenJourney()
        {
            Open();
            var window = GetWindow<TerrainStylePreviewWindow>();
            window.state.AdvancedPage = "planet"; window.SelectPage("advanced");
        }
        public static void OpenVeinStudy()
        {
            Open();
            var window = GetWindow<TerrainStylePreviewWindow>();
            window.state.ShowStudy = true; window.state.AdvancedPage = "study";
            window.SelectPage("advanced");
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("矿洞地图工作台"); minSize = new Vector2(800, 540);
            state = state ?? new TerrainWorkbenchState();
            generation = new TerrainGenerationPreview(flowDraft); flowDraft = generation.Draft;
            if (!string.IsNullOrEmpty(planetId)) generation.PlanetId = planetId;
            generation.JointDraft = state.CandidateMode;
            if (style == null) style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(Root + "Style.asset");
            if (inspected == null) inspected = style;
            commands = new TerrainWorkbenchCommands(this);
            preview = new TerrainWorkbenchPreview(this);
            if (state.CandidateMode && !state.ViewInstalled && state.Version == null) generation.RequestGeneration();
            preview.RequestOpen();
            saveChangesMessage = "窗口仍有未保存草稿。地图、共享表现、本机实验分别保存；临时拆填不保存。";
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.update += Tick;
        }

        public void CreateGUI()
        {
            view?.Dispose(); rootVisualElement.Clear();
            if (generation == null) return;
            view = new TerrainWorkbenchView(this, rootVisualElement);
            view.Build();
        }

        private void OnDisable()
        {
            planetId = generation?.PlanetId;
            EditorApplication.update -= Tick; Undo.undoRedoPerformed -= OnUndo;
            view?.Dispose(); view = null;
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
            state.Page = page;
            if (page != "extras") state.Pick = 0;
            if (page != "advanced") { state.ShowStudy = false; state.ShowJourney = false; preview?.UsePan(); }
            view?.BuildPage(); Refresh();
        }

        internal void DraftChanged()
        {
            generation.JointDraft = true; state.CandidateMode = true;
            state.ViewInstalled = false; state.Version = null; state.CheckedIdentity = "";
            Notice = "草稿已变化；画布仍保留上一版本，点击更新候选后替换。";
            Refresh();
        }

        internal void RebuildPage() => view?.BuildPage();

        internal void Refresh()
        {
            if (generation == null) return;
            hasUnsavedChanges = generation.HasChanges || drafts.HasChanges || TerrainVeinStudySettings.instance.HasChanges;
            view?.Refresh();
        }

        private void OnUndo()
        {
            state.CheckedIdentity = "";
            preview?.RestoreStyleAfterUndo();
            Notice = "撤销／重做已恢复作者输入；更新候选以匹配当前草稿。";
            view?.BuildPage(); Refresh();
        }

        public void ImportLegacyDraft(ExpeditionFlowDraft legacy)
        {
            if (preview.Map.HasChanges) throw new InvalidOperationException("先取消临时拆填，再转移旧航程草稿。");
            generation.Import(legacy); view?.BuildPage(); DraftChanged();
        }

        public override void SaveChanges()
        {
            if (drafts.HasChanges || TerrainVeinStudySettings.instance.HasChanges)
            {
                string domains = (generation.HasChanges ? "地图／航程作者配置\n" : "") +
                    (drafts.HasChanges ? "共享表现资产（影响其他引用地图）\n" : "") +
                    (TerrainVeinStudySettings.instance.HasChanges ? "本机制作实验预设\n" : "");
                if (!EditorUtility.DisplayDialog("保存窗口未保存草稿", domains + "临时拆填不保存。", "保存列出项目", "返回")) return;
            }
            if (generation.HasChanges) commands.SaveDraft();
            if (drafts.HasChanges) commands.SaveStyles(true);
            if (TerrainVeinStudySettings.instance.HasChanges) TerrainVeinStudySettings.instance.SavePreset();
            Refresh();
        }

        public override void DiscardChanges()
        {
            generation.Cancel(); drafts.Clear(); TerrainVeinStudySettings.instance.DiscardPreset();
            preview.Map.Cancel(); state.ViewInstalled = true; state.Version = null;
            state.CandidateMode = false; generation.JointDraft = false; state.CheckedIdentity = "";
            preview.RequestOpen(); view?.BuildPage();
            Notice = "窗口草稿已取消；原资产、新局引用和旧存档未改变。"; Refresh();
        }

        private void Tick()
        {
            if (generation == null || preview == null) return;
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            if (playing) { if (!wasPlaying) preview.Suspend(); wasPlaying = true; Refresh(); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (wasPlaying) { wasPlaying = false; preview.RequestOpen(); }
            try { preview.Tick(); }
            catch (Exception error) { Notice = "预览失败：" + error.Message; preview.CancelPending(); }
            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + .25;
            try
            {
                generation.ObserveExternalChanges();
                string source = generation.Draft.BaselineJson;
                if (observedSource != null && observedSource != source)
                { view?.BuildPage(); if (state.ViewInstalled) preview.RequestOpen(); }
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
