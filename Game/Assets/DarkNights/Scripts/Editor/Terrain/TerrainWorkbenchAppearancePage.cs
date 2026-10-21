using System;
using System.Collections.Generic;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 岩壁／背景／点缀的原生UITK属性页；编辑原有表现草稿并保留逐字段恢复，不直接写共享源资产。
    /// 页面退出释放序列化绑定，保存仍使用原逐字段外部冲突检查并明确列出共享影响。
    /// </summary>
    internal sealed class TerrainWorkbenchAppearancePage : IDisposable
    {
        private readonly TerrainWorkbenchFields form;
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private readonly List<string> labels = new List<string>();
        private SerializedObject serialized;
        private VisualElement inspector;
        private string displayedIdentity;

        internal TerrainWorkbenchAppearancePage(TerrainWorkbenchFields form)
        {
            this.form = form;
            form.Title("画面表现", "只改变表现草稿；共享资产保存独立于地图与本机实验。");
            BuildAssets();
            if (assets.Count == 0) { form.Help("正式样式资产缺失。", HelpBoxMessageType.Error); return; }
            int selected = Math.Max(0, assets.IndexOf(form.Owner.Inspected));
            var picker = new DropdownField("调试资产", labels, selected);
            picker.RegisterValueChangedCallback(change =>
            {
                form.Owner.Inspected = assets[picker.index];
                form.Owner.RebuildPage();
            });
            form.Root.Add(picker); form.Owner.Inspected = assets[selected];
            var source = new ObjectField("原资产") { objectType = typeof(ScriptableObject), allowSceneObjects = false };
            source.SetValueWithoutNotify(assets[selected]); source.SetEnabled(false); form.Root.Add(source);
            form.Help("共享样式会影响其他引用地图。保存前显示具体资产，取消不会写源。");
            inspector = new VisualElement(); form.Root.Add(inspector);
            DrawProperties(assets[selected]);
            form.Button("保存共享表现草稿…", form.Owner.Commands.SaveStyles);
            form.Button("取消表现草稿修改", () =>
            {
                if (!EditorUtility.DisplayDialog("取消表现草稿", "仅取消窗口中的表现修改，地图草稿保留。", "取消表现修改", "返回")) return;
                form.Owner.Styles.Clear(); form.Owner.Preview.RequestStyle(); form.Owner.RebuildPage();
            });
        }

        private void BuildAssets()
        {
            var owner = form.Owner; var style = owner.Style;
            Add("岩壁 / 外轮廓", style);
            if (style == null) return;
            var working = owner.Styles.Draft(style);
            AddModifiers("前景", working.Modifiers);
            var background = working.Background; Add("地下背景", background);
            if (background == null) return;
            var layer = owner.Styles.Draft(background);
            Add("背景点缀生成器", layer.Generator);
            AddModifiers("近层", layer.NearModifiers);
            AddModifiers("中层", layer.MiddleModifiers);
            AddModifiers("深层", layer.DeepModifiers);
        }

        private void Add(string label, ScriptableObject asset)
        { if (asset != null) { assets.Add(asset); labels.Add(label); } }

        private void AddModifiers(string label, CaveModifierAsset[] modifiers)
        {
            if (modifiers == null) return;
            for (int i = 0; i < modifiers.Length; i++) Add(label + " / " + (i + 1) + " / " + modifiers[i]?.name, modifiers[i]);
        }

        private void DrawProperties(ScriptableObject source)
        {
            serialized = new SerializedObject(form.Owner.Styles.Draft(source));
            displayedIdentity = JsonUtility.ToJson(serialized.targetObject);
            var iterator = serialized.GetIterator();
            for (bool next = iterator.NextVisible(true); next; next = iterator.NextVisible(false))
            {
                if (iterator.propertyPath == "m_Script") continue;
                string path = iterator.propertyPath;
                var row = new VisualElement(); inspector.Add(row);
                var field = new PropertyField(iterator.Copy()); field.BindProperty(iterator.Copy()); row.Add(field);
                field.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
                {
                    string current = JsonUtility.ToJson(serialized.targetObject);
                    if (current == displayedIdentity) return;
                    displayedIdentity = current;
                    form.Owner.Preview.RequestStyle(); form.Owner.Refresh();
                });
                var reset = new Button(() =>
                {
                    form.Owner.Execute(() =>
                    {
                        form.Owner.Styles.Reset(source, path); serialized.Update();
                        form.Owner.Preview.RequestStyle();
                    });
                }) { text = "恢复此字段" };
                reset.tooltip = "恢复为打开该草稿时的原资产值；支持Undo。";
                row.Add(reset);
            }
        }

        public void Dispose()
        {
            inspector?.Unbind(); serialized?.Dispose(); serialized = null;
        }
    }
}
