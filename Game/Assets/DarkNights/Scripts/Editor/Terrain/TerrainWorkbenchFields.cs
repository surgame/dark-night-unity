using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 工作台任务属性的原生序列化控件；一页共用同一作者SerializedObject，值与Undo仍归原草稿。
    /// 页面退出时撤销绑定并释放句柄，普通字段变化不重建页面、不吞掉文本焦点。
    /// </summary>
    internal sealed class TerrainWorkbenchFields : IDisposable
    {
        private readonly TerrainStylePreviewWindow owner;
        private readonly SerializedObject data;
        private string identity;
        internal VisualElement Root { get; }
        internal TerrainStylePreviewWindow Owner => owner;
        internal TerrainWorkbenchFields(TerrainStylePreviewWindow owner, VisualElement root)
        {
            this.owner = owner; Root = root;
            data = new SerializedObject(owner.Draft); identity = owner.Draft.Config.CanonicalIdentity();
        }

        internal void Title(string title, string description)
        {
            var label = new Label(title); label.AddToClassList("map-page-title"); Root.Add(label);
            Note(description);
        }

        internal Label Note(string text, VisualElement parent = null)
        {
            var label = new Label(text); label.AddToClassList("map-note"); (parent ?? Root).Add(label); return label;
        }

        internal void Heading(string text)
        {
            var label = new Label(text); label.AddToClassList("map-section"); Root.Add(label);
        }

        internal HelpBox Help(string message, HelpBoxMessageType type = HelpBoxMessageType.Info, VisualElement parent = null)
        {
            var box = new HelpBox(message, type); (parent ?? Root).Add(box); return box;
        }

        internal Button Button(string text, Action action, VisualElement parent = null, bool primary = false)
        {
            var button = new Button(() => owner.Execute(action)) { text = text };
            button.AddToClassList("map-full-button");
            if (primary) button.AddToClassList("map-primary");
            (parent ?? Root).Add(button); return button;
        }

        internal Foldout Fold(string text, bool open = false, VisualElement parent = null)
        {
            var fold = new Foldout { text = text, value = open }; (parent ?? Root).Add(fold); return fold;
        }

        internal PropertyField Field(string path, string label, VisualElement parent = null)
        {
            data.UpdateIfRequiredOrScript();
            var property = data.FindProperty(path);
            if (property == null) throw new InvalidOperationException("作者字段缺失：" + path);
            var field = new PropertyField(property, label);
            field.AddToClassList("map-author-edit");
            field.BindProperty(property);
            field.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                string current = owner.Draft.Config.CanonicalIdentity();
                if (identity == current) return;
                identity = current; owner.DraftChanged();
            });
            (parent ?? Root).Add(field);
            field.SetEnabled(!owner.Preview.Map.HasChanges);
            return field;
        }

        internal void Choices(string path, string[] values, string[] labels)
        {
            data.UpdateIfRequiredOrScript();
            string current = data.FindProperty(path).stringValue;
            var row = new VisualElement(); row.AddToClassList("map-choices"); Root.Add(row);
            for (int i = 0; i < values.Length; i++)
            {
                string choice = values[i];
                var button = Button(labels[i], () =>
                {
                    owner.Commands.Edit("选择地图制作参数", () =>
                    {
                        data.Update(); data.FindProperty(path).stringValue = choice;
                        data.ApplyModifiedPropertiesWithoutUndo();
                    });
                    owner.RebuildPage();
                }, row);
                button.EnableInClassList("map-selected", current == choice);
            }
        }

        internal DropdownField Options(string path, string label, string[] values, string[] labels)
        {
            data.UpdateIfRequiredOrScript();
            int selected = Math.Max(0, Array.IndexOf(values, data.FindProperty(path).stringValue));
            var field = new DropdownField(label, new List<string>(labels), selected);
            field.RegisterValueChangedCallback(change => owner.Execute(() =>
                owner.Commands.Edit("修改" + label, () =>
                {
                    data.Update(); data.FindProperty(path).stringValue = values[field.index];
                    data.ApplyModifiedPropertiesWithoutUndo();
                })));
            field.AddToClassList("map-author-edit"); Root.Add(field); return field;
        }

        internal void Diagnostics()
        {
            foreach (string line in owner.Generation.ContentDiagnostics)
            {
                var label = new Label(line); label.AddToClassList("map-diagnostics"); Root.Add(label);
            }
        }

        internal void ShowWhen(string path, VisualElement target, bool expected = true)
        {
            var property = data.FindProperty(path);
            target.EnableInClassList("map-hidden", property.boolValue != expected);
            target.TrackPropertyValue(property, value => target.EnableInClassList("map-hidden", value.boolValue != expected));
        }

        public void Dispose() { Root.Unbind(); data.Dispose(); }
    }
}
