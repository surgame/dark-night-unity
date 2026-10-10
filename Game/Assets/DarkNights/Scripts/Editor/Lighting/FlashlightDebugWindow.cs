using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.View;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Lighting
{
    /// <summary>
    /// 光照运行时草稿工作台；选中一个真实手电实例即时调参，资产只在独立确认保存入口写回。
    /// 控件重建保留草稿，换目标、关闭、脚本重载及退出 Play 撤除草稿和预览，不拥有业务状态。
    /// </summary>
    public sealed class FlashlightDebugWindow : EditorWindow
    {
        private const string Root = "Assets/DarkNights/Res/Shared/Lighting/Debug/";
        [SerializeField] private ObjectDefinition definition;
        [SerializeField] private LightProfile preset;
        [SerializeField] private SceneLightingProfile sceneProfile;
        [SerializeField] private FlashlightView runtimeTool;
        private readonly FlashlightDebugUndo undo = new FlashlightDebugUndo(new FlashlightDebugHistory());
        private FlashlightDebugDraft draft;
        private SceneLightingProfile sceneDraft;
        private SceneLightingProfile sceneSource;
        private ExplorationLightSettings originalScene;
        private FlashlightDebugScenePreview scenePreview;
        private LightOverridePanel panel;
        private SceneLightingPanel scenePanel;
        private LightMountPanel mountPanel;
        private Label status;
        private string notice;
        private IVisualElementScheduledItem refresh;
        public FlashlightDebugDraft Draft => draft;
        private bool SceneChanged => sceneDraft != null && JsonUtility.ToJson(sceneDraft.Settings) != JsonUtility.ToJson(originalScene);

        [MenuItem("Dark Nights/Debug/手电光照调试")]
        public static void Open() => Open(Selection.activeObject as ObjectDefinition);
        public static void Open(ObjectDefinition selected)
        {
            var window = GetWindow<FlashlightDebugWindow>("光照运行时调参");
            window.minSize = new Vector2(520, 600);
            if (selected != null && selected != window.definition)
            {
                if (!window.AllowDiscard()) return;
                window.ReleaseDrafts(); window.definition = selected; window.preset = LightDefinitionEditor.Preset(selected);
                var matches = FindObjectsByType<FlashlightView>()
                    .Where(tool => ValidTool(tool) && tool.Owner.Definition == selected).ToArray();
                window.runtimeTool = matches.Length == 1 ? matches[0] : null;
            }
            window.CreateGUI(); window.Focus();
        }

        private void OnEnable() { Undo.undoRedoPerformed += Refresh; EditorApplication.playModeStateChanged += PlayChanged; }
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Refresh; EditorApplication.playModeStateChanged -= PlayChanged;
            refresh?.Pause(); ReleaseDrafts();
        }
        private void OnLostFocus() => undo.EndGesture();
        private void PlayChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.ExitingEditMode)
            { ReleaseDrafts(); runtimeTool = null; }
            else { notice = "运行模式已切换，未保存的临时调整已撤除。"; CreateGUI(); }
        }
        private static bool ValidTool(FlashlightView tool) => Application.isPlaying && tool != null && tool.isActiveAndEnabled
            && !EditorUtility.IsPersistent(tool) && tool.Owner != null && tool.BaseLightProfile != null;

        private void EnsureDrafts()
        {
            if (draft != null && !draft.Matches(definition, preset, runtimeTool)) { mountPanel?.Dispose(); draft.Dispose(); draft = null; }
            if (draft == null && (preset != null || LightDefinitionEditor.Config(definition) != null))
            { draft = CreateInstance<FlashlightDebugDraft>(); draft.Initialize(definition, preset, runtimeTool); }
            if (sceneSource != sceneProfile)
            {
                ReleaseScene(); sceneSource = sceneProfile;
                if (sceneSource != null)
                {
                    sceneDraft = Instantiate(sceneSource); sceneDraft.hideFlags = HideFlags.HideAndDontSave;
                    sceneDraft.name = "场景光照运行时副本";
                    originalScene = JsonUtility.FromJson<ExplorationLightSettings>(JsonUtility.ToJson(sceneSource.Settings));
                    scenePreview = new FlashlightDebugScenePreview(this);
                }
            }
        }

        public void CreateGUI()
        {
            refresh?.Pause(); scenePanel?.Dispose(); scenePanel = null; mountPanel?.Dispose(); mountPanel = null; undo.EndGesture();
            rootVisualElement.UnregisterCallback<KeyDownEvent>(HistoryKey, TrickleDown.TrickleDown);
            rootVisualElement.UnregisterCallback<ExecuteCommandEvent>(HistoryCommand, TrickleDown.TrickleDown);
            rootVisualElement.UnregisterCallback<ValidateCommandEvent>(ValidateHistoryCommand, TrickleDown.TrickleDown);
            rootVisualElement.Clear(); rootVisualElement.focusable = true;
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "FlashlightDebug.uxml").CloneTree(rootVisualElement);
            status = rootVisualElement.Q<Label>("status");
            BindSelectors();
            try
            {
                EnsureDrafts(); panel = draft != null ? new LightOverridePanel(draft, undo) : null;
                if (panel != null) rootVisualElement.Q("parameters").Add(panel.Root);
                if (draft != null)
                {
                    mountPanel = new LightMountPanel(draft, undo, () => Save(() => FlashlightDebugSave.Mount(draft)));
                    rootVisualElement.Q("mount").Add(mountPanel.Root);
                }
                if (sceneDraft != null)
                { scenePanel = new SceneLightingPanel(sceneDraft, undo, Refresh); rootVisualElement.Q("sceneParameters").Add(scenePanel.Root); }
            }
            catch (Exception error) { notice = error.Message; }
            rootVisualElement.Q<Button>("saveDefinition").clicked += () => Save(() => FlashlightDebugSave.Definition(draft));
            rootVisualElement.Q<Button>("saveProfile").clicked += () => Save(() => FlashlightDebugSave.Profile(draft));
            rootVisualElement.Q<Button>("saveSceneProfile").clicked += SaveScene;
            rootVisualElement.Q<Button>("openProfile").clicked += () => { if (preset != null) AssetDatabase.OpenAsset(preset); };
            rootVisualElement.Q<Button>("resetRuntime").clicked += () => { draft?.Reset(undo); Refresh(); };
            rootVisualElement.Q<Button>("chooseRuntime").clicked += ChooseRuntime;
            rootVisualElement.Q<Button>("undoRuntime").clicked += () => PerformHistory(false);
            rootVisualElement.Q<Button>("redoRuntime").clicked += () => PerformHistory(true);
            rootVisualElement.RegisterCallback<KeyDownEvent>(HistoryKey, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<ExecuteCommandEvent>(HistoryCommand, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<ValidateCommandEvent>(ValidateHistoryCommand, TrickleDown.TrickleDown);
            refresh = rootVisualElement.schedule.Execute(Refresh).Every(500); Refresh();
        }

        private void BindSelectors()
        {
            var owner = rootVisualElement.Q<ObjectField>("definition"); owner.objectType = typeof(ObjectDefinition); owner.allowSceneObjects = false;
            owner.SetValueWithoutNotify(definition);
            owner.RegisterValueChangedCallback(evt =>
            {
                if (!AllowDiscard()) { owner.SetValueWithoutNotify(definition); return; }
                ReleaseDrafts(); definition = evt.newValue as ObjectDefinition; preset = LightDefinitionEditor.Preset(definition); runtimeTool = null; CreateGUI();
            });
            var profile = rootVisualElement.Q<ObjectField>("profile"); profile.objectType = typeof(LightProfile); profile.allowSceneObjects = false;
            profile.SetValueWithoutNotify(preset);
            profile.RegisterValueChangedCallback(evt =>
            {
                if (!AllowDiscard()) { profile.SetValueWithoutNotify(preset); return; }
                ReleaseDrafts(); preset = evt.newValue as LightProfile; CreateGUI();
            });
            var instance = rootVisualElement.Q<ObjectField>("runtimeTool"); instance.objectType = typeof(FlashlightView); instance.allowSceneObjects = true;
            instance.SetValueWithoutNotify(runtimeTool);
            instance.RegisterValueChangedCallback(evt =>
            {
                var selected = evt.newValue as FlashlightView;
                if (selected != null && !ValidTool(selected)) { instance.SetValueWithoutNotify(runtimeTool); notice = "请选择当前运行中已绑定光照的手电实例。"; Refresh(); return; }
                if (!SelectRuntime(selected)) instance.SetValueWithoutNotify(runtimeTool);
            });
            var scene = rootVisualElement.Q<ObjectField>("sceneProfile"); scene.objectType = typeof(SceneLightingProfile); scene.allowSceneObjects = false;
            scene.SetValueWithoutNotify(sceneProfile);
            scene.RegisterValueChangedCallback(evt =>
            {
                if (!AllowDiscard()) { scene.SetValueWithoutNotify(sceneProfile); return; }
                ReleaseDrafts(); sceneProfile = evt.newValue as SceneLightingProfile; CreateGUI();
            });
        }

        private bool SelectRuntime(FlashlightView selected)
        {
            if (!AllowDiscard()) return false;
            ReleaseDrafts(); runtimeTool = selected;
            if (selected != null) { definition = selected.Owner.Definition; preset = LightDefinitionEditor.Preset(definition); }
            notice = null; CreateGUI(); return true;
        }
        private void ChooseRuntime()
        {
            var menu = new GenericMenu(); menu.AddItem(new GUIContent("仅编辑草稿"), runtimeTool == null, () => SelectRuntime(null));
            foreach (var tool in FindObjectsByType<FlashlightView>().Where(ValidTool))
            {
                var captured = tool;
                menu.AddItem(new GUIContent(tool.Owner.Definition.Name + "  " + tool.Owner.InstanceId), tool == runtimeTool, () => SelectRuntime(captured));
            }
            menu.ShowAsContext();
        }
        private bool AllowDiscard() => !(draft?.HasChanges == true || SceneChanged) || EditorUtility.DisplayDialog("切换调试目标",
            "切换将撤除当前未保存的临时调整。资产不会自动保存。", "切换并丢弃", "返回");

        private void Refresh()
        {
            if (status == null) return;
            if (draft != null && draft.HadRuntimeTool && !draft.ToolAlive || scenePreview?.WorldChanged == true)
            { ReleaseDrafts(); runtimeTool = null; notice = "实例或世界已退休，临时调整已撤除。"; CreateGUI(); return; }
            try
            {
                draft?.ApplyRuntime();
                if (draft != null) { preset = draft.Preset; rootVisualElement.Q<ObjectField>("profile").SetValueWithoutNotify(preset); }
                panel?.Refresh(); mountPanel?.Refresh(); scenePanel?.Refresh(); scenePreview?.Apply(sceneSource, sceneDraft);
            }
            catch (Exception error) { notice = error.Message; }
            status.text = (notice != null ? notice + "\n" : "") + (draft?.ToolAlive == true ? "当前运行手电：" + runtimeTool.Owner.InstanceId
                : "当前为临时草稿／Prefab 预览；选择运行手电可实时查看效果。")
                + "\n修改只写临时副本。确认保存后才写入资产；关闭、换目标或退出 Play 丢弃未保存调整。";
            rootVisualElement.Q<Button>("saveDefinition").SetEnabled(draft != null && draft.HasLightChanges && EditorUtility.IsPersistent(definition));
            rootVisualElement.Q<Button>("saveProfile").SetEnabled(draft != null && draft.Values.Mask != LightOverrideMask.None && EditorUtility.IsPersistent(preset));
            rootVisualElement.Q<Button>("saveSceneProfile").SetEnabled(SceneChanged && EditorUtility.IsPersistent(sceneSource));
            rootVisualElement.Q<Button>("resetRuntime").SetEnabled(draft != null && draft.Values.Mask != LightOverrideMask.None);
            rootVisualElement.Q<Button>("undoRuntime").SetEnabled(undo.CanUndo);
            rootVisualElement.Q<Button>("redoRuntime").SetEnabled(undo.CanRedo);
        }
        private void Save(Func<bool> write)
        {
            rootVisualElement.Focus(); undo.EndGesture(); mountPanel?.Dispose();
            try { if (write()) { undo.ClearTemporary(); if (draft != null) preset = draft.Preset; notice = "已确认保存到目标资产。"; } }
            catch (Exception error) { notice = error.Message; }
            CreateGUI();
        }
        private void SaveScene() => Save(() =>
        {
            if (!FlashlightDebugSave.Scene(sceneSource, sceneDraft, originalScene)) return false;
            Undo.ClearUndo(sceneDraft); sceneDraft.Settings = JsonUtility.FromJson<ExplorationLightSettings>(JsonUtility.ToJson(sceneSource.Settings));
            originalScene = JsonUtility.FromJson<ExplorationLightSettings>(JsonUtility.ToJson(sceneSource.Settings)); return true;
        });
        private void ReleaseScene()
        {
            scenePanel?.Dispose(); scenePanel = null; scenePreview?.Dispose(); scenePreview = null;
            if (sceneDraft != null) { Undo.ClearUndo(sceneDraft); DestroyImmediate(sceneDraft); }
            sceneDraft = null; sceneSource = null; originalScene = null;
        }
        private void ReleaseDrafts()
        {
            undo.ClearTemporary(); mountPanel?.Dispose(); mountPanel = null; panel = null;
            if (draft != null) draft.Dispose(); draft = null; ReleaseScene();
        }
        private void PerformHistory(bool redo) { rootVisualElement.Focus(); undo.EndGesture(); if (redo) undo.RedoTemporary(); else undo.UndoTemporary(); Refresh(); }
        private void HistoryKey(KeyDownEvent evt)
        {
            if (!evt.actionKey || evt.altKey) return;
            if (evt.keyCode == KeyCode.Z) PerformHistory(evt.shiftKey); else if (evt.keyCode == KeyCode.Y) PerformHistory(true); else return;
            evt.StopImmediatePropagation(); rootVisualElement.focusController?.IgnoreEvent(evt);
        }
        private void HistoryCommand(ExecuteCommandEvent evt)
        {
            if (evt.commandName == "Undo") PerformHistory(false); else if (evt.commandName == "Redo") PerformHistory(true); else return;
            evt.StopImmediatePropagation(); rootVisualElement.focusController?.IgnoreEvent(evt);
        }
        private void ValidateHistoryCommand(ValidateCommandEvent evt)
        {
            if (evt.commandName != "Undo" && evt.commandName != "Redo") return;
            evt.StopImmediatePropagation(); rootVisualElement.focusController?.IgnoreEvent(evt);
        }
    }
}
