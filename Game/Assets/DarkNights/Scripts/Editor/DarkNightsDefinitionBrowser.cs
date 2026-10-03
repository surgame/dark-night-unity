using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 游戏 Res 范围的原生 Definition 浏览器；按 Key、名称和路径筛选，选择后直接编辑原始资产。
    /// 列表只是作者资产引用，不创建实例或分配身份；Editor 缓存、项目事件与刷新随面板释放。
    /// </summary>
    internal sealed class DarkNightsDefinitionBrowser : VisualElement, IDisposable
    {
        private readonly List<ObjectDefinition> definitions = new List<ObjectDefinition>();
        private readonly List<ObjectDefinition> filtered = new List<ObjectDefinition>();
        private readonly ListView list;
        private readonly VisualElement editorArea = new VisualElement();
        private readonly Label state = new Label();
        private readonly IVisualElementScheduledItem refresh;
        private DarkNightsDefinitionEditor inspector;
        private readonly DarkNightsDefinitionBrowserState selection;
        private ObjectDefinition selected { get => selection.Selected; set => selection.Selected = value; }
        private string query { get => selection.Query; set => selection.Query = value; }
        private bool disposed, subscribed;

        internal DarkNightsDefinitionBrowser() : this(new DarkNightsDefinitionBrowserState()) { }
        internal DarkNightsDefinitionBrowser(DarkNightsDefinitionBrowserState selection)
        {
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
            AddToClassList("dn-definition-browser");
            Add(new HelpBox("仅浏览游戏 Res 中的原生 Definition。身份与能力沿用 YYGC 编辑器保护，保存只写当前资产。", HelpBoxMessageType.Info));
            var search = new ToolbarSearchField { name = "definition-search", value = query };
            search.RegisterValueChangedCallback(change => { query = change.newValue; Filter(); }); Add(search);
            list = new ListView { name = "definition-list", itemsSource = filtered, fixedItemHeight = 34, selectionType = SelectionType.Single,
                makeItem = () => new Label(), bindItem = (element, index) =>
                {
                    var definition = filtered[index]; var label = (Label)element;
                    label.text = definition.Key + " · " + definition.name;
                    label.tooltip = AssetDatabase.GetAssetPath(definition) + "\n" + definition.Guid;
                } };
            list.selectionChanged += items => Select(items.OfType<ObjectDefinition>().FirstOrDefault());
            var body = new VisualElement(); body.AddToClassList("dn-browser-body");
            var listPane = new VisualElement(); listPane.AddToClassList("dn-browser-list");
            listPane.Add(list); listPane.Add(state); body.Add(listPane);
            editorArea.AddToClassList("dn-browser-editor"); body.Add(editorArea); Add(body);
            RegisterCallback<GeometryChangedEvent>(change => EnableInClassList("dn-browser-compact", change.newRect.width < 760));
            RegisterCallback<AttachToPanelEvent>(_ => { if (!subscribed) { EditorApplication.projectChanged += Reload; subscribed = true; Reload(); } });
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            refresh = schedule.Execute(UpdateState).Every(250); Reload();
        }

        private void Reload()
        {
            if (disposed) return;
            definitions.Clear();
            definitions.AddRange(AssetDatabase.FindAssets("t:ObjectDefinition", new[] { "Assets/DarkNights/Res" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ObjectDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(definition => definition != null).OrderBy(definition => definition.Key, StringComparer.Ordinal));
            Filter();
        }

        private void Filter()
        {
            var desired = selected;
            filtered.Clear();
            filtered.AddRange(definitions.Where(definition => string.IsNullOrWhiteSpace(query) ||
                (definition.Key + " " + definition.name + " " + AssetDatabase.GetAssetPath(definition))
                    .IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0));
            list.SetSelectionWithoutNotify(Array.Empty<int>()); list.Rebuild();
            if (filtered.Count == 0) { Select(null); return; }
            int index = filtered.IndexOf(desired); list.SetSelectionWithoutNotify(new[] { Math.Max(0, index) });
            Select(filtered[list.selectedIndex]);
        }

        private void Select(ObjectDefinition definition)
        {
            if (selected == definition && inspector != null && inspector.Target == definition) { UpdateState(); return; }
            ClearInspector(); editorArea.Clear();
            if (definition == null) { UpdateState(); return; }
            selected = definition;
            var source = new ObjectField("原始 Definition") { value = selected, objectType = typeof(ObjectDefinition), allowSceneObjects = false };
            source.SetEnabled(false); editorArea.Add(source);
            editorArea.Add(new Button(() => DarkNightsWorkbenchWindow.Locate(selected)) { text = "在 Project 中定位" });
            inspector = new DarkNightsDefinitionEditor(selected); editorArea.Add(inspector);
            UpdateState();
        }

        private void UpdateState()
        {
            if (disposed) return;
            string suffix = inspector == null ? "无匹配结果。" : EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                ? "Play／编译期间禁止编辑与保存。" : EditorUtility.IsDirty(selected) ? "当前资产有未保存修改。" : "当前资产已保存。";
            state.text = filtered.Count + " / " + definitions.Count + " 个 Definition；" + suffix;
        }
        private void Unsubscribe() { if (subscribed) EditorApplication.projectChanged -= Reload; subscribed = false; }
        private void ClearInspector() { inspector?.Dispose(); inspector = null; }
        public void Dispose() { if (disposed) return; disposed = true; Unsubscribe(); refresh.Pause(); ClearInspector(); Clear(); }
    }
}
