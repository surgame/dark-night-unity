using System;
using System.Linq;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Lighting
{
    /// <summary>UITK 光照调参台的草稿与控件生命周期；纵向页签不重建状态，单灯、场景、灯口和预览记录只从统一确认入口保存。</summary>
    public sealed class FlashlightDebugWindow : EditorWindow
    {
        private const string Root = "Assets/DarkNights/Res/Shared/Lighting/Debug/";
        [SerializeField] private ObjectDefinition definition;
        [SerializeField] private LightProfile preset;
        [SerializeField] private SceneLightingProfile sceneProfile;
        [SerializeField] private FlashlightView runtimeTool;
        [SerializeField] private string activePage = "Beam";
        [SerializeField] private bool sharedTarget;
        private readonly FlashlightDebugUndo undo = new FlashlightDebugUndo(new FlashlightDebugHistory());
        private FlashlightDebugDraft draft;
        private SceneLightingProfile sceneDraft, sceneSource;
        private ExplorationLightSettings originalScene;
        private FlashlightDebugScenePreview scenePreview;
        private FlashlightPreviewDraft previewDraft;
        private FlashlightPreviewPane previewPane;
        private FlashlightDebugSources sources;
        private FlashlightDebugNavigation navigation;
        private LightOverridePanel panel;
        private SceneLightingPanel scenePanel;
        private LightMountPanel mountPanel;
        private Label status;
        private string notice;
        private IVisualElementScheduledItem refresh;
        public FlashlightDebugDraft Draft => draft;
        internal ObjectDefinition DefinitionSource => definition;
        internal LightProfile PresetSource => preset;
        internal FlashlightView RuntimeSource => runtimeTool;
        internal SceneLightingProfile SceneSource => sceneProfile;
        internal SceneLightingProfile SceneDraft => sceneDraft;
        internal bool SharedTarget { get => sharedTarget; set => sharedTarget = value; }
        private bool SceneChanged => sceneDraft != null && JsonUtility.ToJson(sceneDraft.Settings) != JsonUtility.ToJson(originalScene);

        [MenuItem("Dark Nights/Debug/手电光照调试")]
        public static void Open() => Open(Selection.activeObject as ObjectDefinition);
        public static void Open(ObjectDefinition selected)
        {
            var window = GetWindow<FlashlightDebugWindow>("光照调参台");
            window.minSize = new Vector2(840, 680);
            if (selected != null && selected != window.definition) window.ChangeDefinition(selected);
            else if (window.definition == null && window.preset == null)
            {
                window.definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/Flashlight/Flashlight.asset");
                window.preset = LightDefinitionEditor.Preset(window.definition);
            }
            if (window.sceneProfile == null) window.sceneProfile = AssetDatabase.LoadAssetAtPath<SceneLightingProfile>("Assets/DarkNights/Res/Shared/Lighting/Profiles/PinewatchLighting.asset");
            window.CreateGUI(); window.Focus();
        }
        private void OnEnable() { Undo.undoRedoPerformed += Refresh; EditorApplication.playModeStateChanged += PlayChanged; }
        private void OnDisable()
        { Undo.undoRedoPerformed -= Refresh; EditorApplication.playModeStateChanged -= PlayChanged; DisposeControls(); ReleaseLight(); ReleaseScene(); ReleasePreviewDraft(); }
        private void OnLostFocus() => undo.EndGesture();
        private void PlayChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.ExitingEditMode)
            { DisposeControls(); ReleaseLight(); ReleaseScene(); runtimeTool = null; }
            else { notice = "运行模式已切换，未保存的单灯／场景临时效果已撤除；右侧草稿仍可保存为记录。"; CreateGUI(); }
        }
        private void EnsureDrafts()
        {
            if (draft != null && !draft.Matches(definition, preset, runtimeTool)) ReleaseLight();
            if (draft == null && (preset != null || LightDefinitionEditor.Config(definition) != null))
            { draft = CreateInstance<FlashlightDebugDraft>(); draft.Initialize(definition, preset, runtimeTool); }
            if (sceneSource != sceneProfile)
            {
                ReleaseScene(); sceneSource = sceneProfile;
                if (sceneSource != null)
                {
                    sceneDraft = Instantiate(sceneSource); sceneDraft.hideFlags = HideFlags.HideAndDontSave;
                    originalScene = JsonUtility.FromJson<ExplorationLightSettings>(JsonUtility.ToJson(sceneSource.Settings));
                    scenePreview = new FlashlightDebugScenePreview(this);
                }
            }
            if (previewDraft == null)
            {
                previewDraft = CreateInstance<FlashlightPreviewDraft>();
                try { previewDraft.Initialize(FlashlightDebugRecordStore.LastPreview()); }
                catch (Exception error) { previewDraft.Initialize(new FlashlightPreviewSettings()); notice = "最近预览未载入，原文件保留：" + error.Message; }
            }
        }
        public void CreateGUI()
        {
            DisposeControls(); undo.EndGesture();
            rootVisualElement.UnregisterCallback<KeyDownEvent>(HistoryKey, TrickleDown.TrickleDown);
            rootVisualElement.UnregisterCallback<ExecuteCommandEvent>(HistoryCommand, TrickleDown.TrickleDown);
            rootVisualElement.UnregisterCallback<ValidateCommandEvent>(ValidateHistoryCommand, TrickleDown.TrickleDown);
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(GeometryChanged);
            rootVisualElement.Clear(); rootVisualElement.focusable = true;
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "FlashlightDebug.uxml");
            if (tree == null) throw new InvalidOperationException("光照调参台 UXML 不可用。");
            tree.CloneTree(rootVisualElement); status = rootVisualElement.Q<Label>("status");
            try
            {
                EnsureDrafts(); sources = new FlashlightDebugSources(this, rootVisualElement);
                navigation = new FlashlightDebugNavigation(rootVisualElement, activePage, page => activePage = page);
                panel = draft != null ? new LightOverridePanel(draft, undo, true) : null;
                if (panel != null)
                    foreach (string group in new[] { "beam", "bounce", "fill" }) rootVisualElement.Q(group + "Parameters").Add(panel.Group(group));
                if (draft != null) { mountPanel = new LightMountPanel(draft, undo); rootVisualElement.Q("mount").Add(mountPanel.Root); }
                if (sceneDraft != null) { scenePanel = new SceneLightingPanel(sceneDraft, undo, Refresh); rootVisualElement.Q("sceneParameters").Add(scenePanel.Root); }
                previewPane = new FlashlightPreviewPane(this, rootVisualElement, previewDraft, undo);
                new FlashlightDebugRecordsPanel(this, rootVisualElement.Q("records"));
            }
            catch (Exception error) { notice = error.Message; }
            rootVisualElement.Q<Button>("saveAll").clicked += () => SaveAll();
            rootVisualElement.Q<Button>("undoRuntime").clicked += () => PerformHistory(false);
            rootVisualElement.Q<Button>("redoRuntime").clicked += () => PerformHistory(true);
            rootVisualElement.RegisterCallback<KeyDownEvent>(HistoryKey, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<ExecuteCommandEvent>(HistoryCommand, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<ValidateCommandEvent>(ValidateHistoryCommand, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(GeometryChanged);
            rootVisualElement.EnableInClassList("compact", position.width < 1000);
            refresh = rootVisualElement.schedule.Execute(Refresh).Every(500); Refresh();
        }
        private void GeometryChanged(GeometryChangedEvent evt) => rootVisualElement.EnableInClassList("compact", evt.newRect.width < 1000);
        internal void RefreshDrafts() => Refresh();
        private void Refresh()
        {
            if (status == null) return;
            if (draft != null && draft.HadRuntimeTool && !draft.ToolAlive || scenePreview?.WorldChanged == true)
            { ReleaseLight(); ReleaseScene(); runtimeTool = null; notice = "实例或世界已退休，关联临时效果已撤除。"; CreateGUI(); return; }
            try
            {
                draft?.ApplyRuntime(); if (draft != null) preset = draft.Preset;
                panel?.Refresh(); mountPanel?.Refresh(); scenePanel?.Refresh(); sources?.Refresh(); previewPane?.Refresh();
                scenePreview?.Apply(sceneSource, sceneDraft);
            }
            catch (Exception error) { notice = error.Message; }
            status.text = string.Join(" ", new[] { notice, panel?.Feedback }.Where(text => !string.IsNullOrEmpty(text)));
            status.EnableInClassList("is-hidden", string.IsNullOrEmpty(status.text));
            rootVisualElement.Q<Label>("targetTitle").text = definition != null ? definition.Name : preset != null ? preset.name : "选择编辑来源";
            int count = draft == null ? 0 : LightParameterSchema.Fields.Count(item => draft.Values.Has(LightParameterSchema.Mask(item.Name)));
            rootVisualElement.Q<Label>("dirtySummary").text = "待保存：单灯 " + count + (draft?.HasPresetChange == true ? " + 预设引用" : "") +
                " · 场景 " + (SceneChanged ? "已改" : "无") + " · 灯口 " + (draft?.HasMountChanges == true ? "已改" : "无") + " · 预览 " + (previewDraft?.HasChanges == true ? "已改" : "可记录快照");
            if (navigation != null)
            {
                foreach (string group in new[] { "beam", "bounce", "fill" })
                    navigation.Changed(char.ToUpperInvariant(group[0]) + group.Substring(1), draft != null && LightParameterSchema.Fields.Any(item => LightParameterSchema.Group(item.Name) == group && draft.Values.Has(LightParameterSchema.Mask(item.Name))));
                navigation.Changed("Scene", SceneChanged); navigation.Changed("Mount", draft?.HasMountChanges == true);
                navigation.Changed("Preset", draft?.HasPresetChange == true); navigation.Changed("History", previewDraft?.HasChanges == true);
            }
            rootVisualElement.Q<Button>("undoRuntime").SetEnabled(undo.CanUndo); rootVisualElement.Q<Button>("redoRuntime").SetEnabled(undo.CanRedo);
            rootVisualElement.Q<Button>("saveAll").SetEnabled(previewDraft != null && !EditorApplication.isCompiling && !EditorApplication.isUpdating);
            hasUnsavedChanges = previewDraft?.HasChanges == true || SceneChanged || draft?.HasChanges == true &&
                (EditorUtility.IsPersistent(definition) || EditorUtility.IsPersistent(preset));
            saveChangesMessage = "单灯、场景、灯口或右侧预览仍有未保存调整。保存会显示完整目标清单并新增调试记录。";
        }
        internal void ChangePreset(LightProfile selected)
        {
            try { draft?.SelectPreset(selected, undo); preset = selected; if (draft == null) CreateGUI(); else Refresh(); }
            catch (Exception error) { notice = error.Message; Refresh(); }
        }
        private bool Discard(bool changed, string scope) => !changed || EditorUtility.DisplayDialog("切换调参来源", "仅丢弃" + scope + "的未保存草稿；其他范围保留。", "切换并丢弃", "返回");
        internal void ChangeDefinition(ObjectDefinition selected)
        {
            if (selected != null && LightDefinitionEditor.Config(selected) == null) { notice = "该 Definition 没有照明道具能力。"; Refresh(); return; }
            if (!Discard(draft?.HasChanges == true, "当前单灯与灯口")) return;
            ReleaseLight(); definition = selected; preset = LightDefinitionEditor.Preset(selected); runtimeTool = null; notice = null; CreateGUI();
        }
        internal void ChangeRuntime(FlashlightView selected)
        {
            if (selected != null && !FlashlightDebugSources.Valid(selected)) { notice = "请选择当前运行中已绑定照明的手电实例。"; Refresh(); return; }
            if (!Discard(draft?.HasChanges == true, "当前单灯与灯口")) return;
            ReleaseLight(); runtimeTool = selected;
            if (selected != null) { definition = selected.Owner.Definition; preset = LightDefinitionEditor.Preset(definition); }
            notice = null; CreateGUI();
        }
        internal void ChangeScene(SceneLightingProfile selected)
        {
            if (!Discard(SceneChanged, "当前场景配置")) return;
            ReleaseScene(); sceneProfile = selected; notice = null; CreateGUI();
        }
        internal void LoadRecord(FlashlightDebugRecord record)
        {
            record.Validate();
            var nextDefinition = FlashlightDebugRecord.Asset<ObjectDefinition>(record.DefinitionGuid);
            var nextProfile = FlashlightDebugRecord.Asset<LightProfile>(record.ProfileGuid);
            var nextScene = FlashlightDebugRecord.Asset<SceneLightingProfile>(record.SceneGuid);
            if (record.EffectiveLight != null && nextProfile == null && LightDefinitionEditor.Config(nextDefinition) == null)
                throw new InvalidOperationException("记录没有可恢复的单灯来源，当前草稿保留。");
            if (!Discard(draft?.HasChanges == true || SceneChanged || previewDraft?.HasChanges == true, "当前所有调试")) return;
            DisposeControls(); ReleaseLight(); ReleaseScene();
            definition = nextDefinition; preset = nextProfile; sceneProfile = nextScene; runtimeTool = null; sharedTarget = record.SharedTarget;
            EnsureDrafts(); if (record.EffectiveLight != null) draft.LoadRecord(record);
            if (record.SceneSettings != null && sceneDraft != null) sceneDraft.Settings = JsonUtility.FromJson<ExplorationLightSettings>(JsonUtility.ToJson(record.SceneSettings));
            previewDraft.Load(record.Preview, record.Name); undo.ClearTemporary();
            notice = "已载入记录到临时草稿；尚未写回作者资产。"; CreateGUI();
        }
        private bool SaveAll()
        {
            rootVisualElement.Focus(); undo.EndGesture();
            try
            {
                var record = FlashlightDebugRecord.Capture(draft, sceneDraft, sceneSource, previewDraft, sharedTarget);
                record.Preview.Angle = previewPane?.CurrentAngle ?? previewDraft.Settings.Angle;
                if (!new FlashlightDebugSaveBatch(draft, sceneSource, sceneDraft, originalScene, record, sharedTarget).Execute()) return false;
                if (sceneDraft != null) { sceneDraft.Settings = JsonUtility.FromJson<ExplorationLightSettings>(JsonUtility.ToJson(sceneSource.Settings)); originalScene = JsonUtility.FromJson<ExplorationLightSettings>(JsonUtility.ToJson(sceneSource.Settings)); }
                previewDraft.AcceptSaved(); undo.ClearTemporary(); notice = "全部保存完成；已新增调试记录：" + record.Name;
                hasUnsavedChanges = false; CreateGUI(); return true;
            }
            catch (Exception error) { notice = error.Message; Refresh(); return false; }
        }
        public override void SaveChanges() { if (SaveAll()) base.SaveChanges(); }
        public override void DiscardChanges() { ReleaseLight(); ReleaseScene(); ReleasePreviewDraft(); base.DiscardChanges(); }
        private void DisposeControls()
        { refresh?.Pause(); refresh = null; previewPane?.Dispose(); previewPane = null; scenePanel?.Dispose(); scenePanel = null; mountPanel?.Dispose(); mountPanel = null; panel = null; sources = null; navigation = null; }
        private void ReleaseLight() { mountPanel?.Dispose(); mountPanel = null; if (draft != null) { undo.RemoveTemporary(draft); draft.Dispose(); } draft = null; }
        private void ReleaseScene()
        { scenePreview?.Dispose(); scenePreview = null; if (sceneDraft != null) { undo.RemoveTemporary(sceneDraft); DestroyImmediate(sceneDraft); } sceneDraft = null; sceneSource = null; originalScene = null; }
        private void ReleasePreviewDraft() { if (previewDraft != null) { undo.RemoveTemporary(previewDraft); DestroyImmediate(previewDraft); } previewDraft = null; }
        private void PerformHistory(bool redo) { rootVisualElement.Focus(); undo.EndGesture(); if (redo) undo.RedoTemporary(); else undo.UndoTemporary(); Refresh(); }
        private void HistoryKey(KeyDownEvent evt)
        {
            if (!evt.actionKey || evt.altKey) return;
            if (evt.keyCode == KeyCode.S) SaveAll(); else if (evt.keyCode == KeyCode.Z) PerformHistory(evt.shiftKey); else if (evt.keyCode == KeyCode.Y) PerformHistory(true); else return;
            evt.StopImmediatePropagation(); rootVisualElement.focusController?.IgnoreEvent(evt);
        }
        private void HistoryCommand(ExecuteCommandEvent evt)
        {
            if (evt.commandName == "Undo") PerformHistory(false); else if (evt.commandName == "Redo") PerformHistory(true); else if (evt.commandName == "Save") SaveAll(); else return;
            evt.StopImmediatePropagation(); rootVisualElement.focusController?.IgnoreEvent(evt);
        }
        private void ValidateHistoryCommand(ValidateCommandEvent evt)
        {
            if (evt.commandName != "Undo" && evt.commandName != "Redo" && evt.commandName != "Save") return;
            evt.StopImmediatePropagation(); rootVisualElement.focusController?.IgnoreEvent(evt);
        }
    }
}
