using System;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 原生 YYGC 配置工坊编辑区的嵌入宿主；复用能力与共享配置绘制，不调用普通 Inspector 的隐式 NetType 同步。
    /// 仅在用户编辑能力或显式保存时同步依赖配置，载入与焦点变化不写资产；属性树和序列化句柄随宿主释放。
    /// </summary>
    internal sealed class DarkNightsDefinitionEditor : VisualElement, IDisposable
    {
        private readonly WorkshopInspectorPresenter presenter = new WorkshopInspectorPresenter();
        private readonly SerializedObject serialized;
        private readonly IVisualElementScheduledItem refresh;
        private readonly Label state = new Label();
        private bool disposed;
        private bool subscribed;
        internal ObjectDefinition Target { get; }
        internal string Error { get; private set; } = "";

        internal DarkNightsDefinitionEditor(ObjectDefinition target, bool showSave = true)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            serialized = new SerializedObject(target);
            AddPane("基本定义与身份", "imgui-basic", false);
            var capabilities = new Foldout { text = "能力装配", value = false, name = "slot-pane" };
            capabilities.Add(new IMGUIContainer { name = "imgui-slots" });
            capabilities.Add(new IMGUIContainer { name = "imgui-left" }); Add(capabilities);
            AddPane("共享配置 SharedConfigs", "imgui-right", true);
            presenter.Initialize(this); presenter.LoadTarget(target, serialized);
            presenter.OnDataModified += OnModified;
            foreach (var container in this.Query<IMGUIContainer>().ToList())
            {
                var draw = container.onGUIHandler;
                container.onGUIHandler = () =>
                {
                    if (disposed || Target == null) return;
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                    { presenter.UpdateTrees(); draw?.Invoke(); }
                };
            }
            Add(state);
            if (showSave) Add(new Button(() => Save()) { text = "保存当前 Definition" });
            RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            UpdateState(); refresh = schedule.Execute(UpdateState).Every(250);
        }

        private void AddPane(string label, string name, bool expanded)
        {
            var foldout = new Foldout { text = label, value = expanded };
            foldout.Add(new IMGUIContainer { name = name }); Add(foldout);
        }
        private void OnModified()
        {
            if (disposed || Target == null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            WorkshopConfigService.AutoSyncAndCleanConfigs(Target, false, presenter.UpdateTrees);
            EditorUtility.SetDirty(Target); presenter.RepaintAll();
        }
        internal bool Save() => TrySave(null);
        internal bool TrySave(Action<ObjectDefinition> validate)
        {
            if (disposed || Target == null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            { Error = "当前编辑状态不允许保存。"; return false; }
            if (!AssetDatabase.Contains(Target)) { Error = "当前 Definition 尚未保存为资产，不能执行资产保存。"; return false; }
            try
            {
                serialized.ApplyModifiedProperties();
                validate?.Invoke(Target);
                WorkshopConfigService.AutoSyncAndCleanConfigs(Target, false, presenter.UpdateTrees);
                ObjectDefinitionSaveUtility.SaveDefinition(Target, WorkshopConfigService.GetRequiredConfigTypes(Target));
                serialized.Update(); presenter.UpdateTrees(); presenter.RepaintAll(); Error = ""; return true;
            }
            catch (Exception error) { Error = error.Message; state.text = "保存未完成：" + Error; return false; }
        }
        private void UpdateState()
        {
            if (disposed) return;
            bool blocked = EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling;
            SetEnabled(!blocked);
            state.text = blocked ? "Play／编译期间禁止编辑与保存。" : Error.Length > 0 ? "保存未完成：" + Error
                : Target != null && EditorUtility.IsDirty(Target) ? "当前原始资产有未保存修改。" : "当前原始资产已保存。";
        }
        private void Subscribe()
        {
            if (disposed || subscribed) return;
            Undo.undoRedoPerformed += OnUndoRedo; EditorApplication.playModeStateChanged += OnPlayMode;
            subscribed = true; OnUndoRedo(); UpdateState();
        }
        private void Unsubscribe()
        {
            if (!subscribed) return;
            Undo.undoRedoPerformed -= OnUndoRedo; EditorApplication.playModeStateChanged -= OnPlayMode; subscribed = false;
        }
        private void OnPlayMode(PlayModeStateChange _) => UpdateState();
        private void OnUndoRedo()
        {
            if (disposed || Target == null) return;
            serialized.Update(); presenter.UpdateTrees(); presenter.RepaintAll();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Unsubscribe(); refresh.Pause(); presenter.OnDataModified -= OnModified;
            presenter.DisposeTrees(); serialized.Dispose(); Clear();
        }
    }
}
