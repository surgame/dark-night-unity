using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 主工作台单个业务页的只读摘要与延迟扩展；只组合原有操作，不拥有作者配置或运行状态。
    /// 扩展首次展开才创建Inspector与辅助工具，切页或关闭时统一释放本页持有的订阅和编辑句柄。
    /// </summary>
    internal sealed class DarkNightsWorkbenchPage : VisualElement, IDisposable
    {
        private readonly Action<Action> execute;
        private readonly List<IDisposable> owned = new List<IDisposable>();
        private readonly List<Action<DarkNightsWorkbenchPage>> extensions = new List<Action<DarkNightsWorkbenchPage>>();

        internal DarkNightsWorkbenchPage(Action<Action> execute)
        {
            this.execute = execute;
            AddToClassList("dn-module");
        }

        internal void Note(string text)
        {
            var label = new Label(text); label.AddToClassList("dn-description"); Add(label);
        }

        internal void Heading(string title)
        {
            var label = new Label(title); label.AddToClassList("dn-section-title"); Add(label);
        }

        internal Label Value(string title, string value)
        {
            var row = new VisualElement(); row.AddToClassList("dn-summary-row");
            var caption = new Label(title); caption.AddToClassList("dn-summary-key"); row.Add(caption);
            var label = new Label(value); label.AddToClassList("dn-summary-value"); row.Add(label); Add(row);
            return label;
        }

        internal void Asset(string title, UnityEngine.Object asset)
        {
            var row = new VisualElement(); row.AddToClassList("dn-asset-row");
            var field = new ObjectField(title) { value = asset, allowSceneObjects = true };
            field.SetEnabled(false); row.Add(field);
            var button = Action("定位", () => DarkNightsWorkbenchWindow.Locate(asset), false);
            button.EnableInClassList("dn-missing", asset == null);
            row.Add(button); Add(row);
        }

        internal Button Action(string title, Action action, bool edits = true)
        {
            var button = new Button(() => execute(() =>
            {
                if (!edits || !DarkNightsNativeWorkspace.Blocked) action();
            })) { text = title, userData = edits ? DarkNightsWorkbenchEntryKind.Tool : DarkNightsWorkbenchEntryKind.Reference };
            button.AddToClassList("dn-native-action"); Add(button); return button;
        }

        internal void Definition(string title, string path)
        {
            var definition = AssetDatabase.LoadAssetAtPath<GameCore.Objects.Definition.ObjectDefinition>(path);
            var button = Action(title, () => DarkNightsNativeWorkspace.Workshop(definition));
            button.tooltip = path; button.EnableInClassList("dn-missing", definition == null);
        }

        internal void Entry(DarkNightsWorkbenchEntry entry)
        {
            if (entry.Kind == DarkNightsWorkbenchEntryKind.Editor)
            {
                var source = new DarkNightsWorkbenchSources(entry, "", _ => { });
                Own(source); Add(source); return;
            }
            Add(DarkNightsWorkbenchCards.Create(entry, () => execute(() =>
            {
                if (DarkNightsNativeWorkspace.CanOpen(entry.Kind)) entry.OpenEditor?.Invoke();
            })));
        }

        internal void Extend(Action<DarkNightsWorkbenchPage> build) => extensions.Add(build);
        internal void Own(IDisposable value) => owned.Add(value);

        internal void Finish(bool expanded)
        {
            if (extensions.Count == 0) return;
            var foldout = new Foldout { text = "扩展", name = "module-extension", value = false };
            foldout.AddToClassList("dn-module-extension");
            bool built = false;
            void Build()
            {
                if (built) return;
                built = true;
                var child = new DarkNightsWorkbenchPage(execute); Own(child); foldout.Add(child);
                foreach (var extension in extensions) extension(child);
            }
            foldout.RegisterValueChangedCallback(change =>
            {
                if (change.target == foldout && change.newValue) execute(Build);
            });
            Add(foldout);
            if (expanded) { Build(); foldout.value = true; }
        }

        public void Dispose()
        {
            foreach (var value in owned) value.Dispose();
            owned.Clear(); extensions.Clear(); Clear();
        }
    }
}
