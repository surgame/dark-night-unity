using System;
using System.Linq;
using DarkNights.Runtime.Objects;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地形步骤的状态卡片；启停始终显示，参数继续由 Odin PropertyTree 编辑 SerializeReference 字段。
    /// 启停、类型及顺序只写同一航程草稿并支持 Undo，显式保存后才影响作者资产和正式生成。
    /// </summary>
    internal sealed class TerrainModifierConfigDrawer : IDisposable
    {
        private readonly ExpeditionFlowDraft draft;
        private SerializedObject serialized;
        private PropertyTree tree;

        public TerrainModifierConfigDrawer(ExpeditionFlowDraft draft)
        {
            this.draft = draft ?? throw new ArgumentNullException(nameof(draft));
            Rebind();
        }

        public void Rebind()
        {
            tree?.Dispose(); serialized?.Dispose();
            serialized = new SerializedObject(draft);
            tree = PropertyTree.Create(serialized);
        }

        public void Draw(Action changed)
        {
            tree.UpdateTree();
            string before = draft.Config.CanonicalIdentity();
            var items = draft.Config.Modifiers;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Modifiers · " + items.Count(m => m?.Enabled == true) + "/" + items.Count + " 已启用",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField("修改为草稿 · 保存全部 / Ctrl+S 后生效", EditorStyles.wordWrappedMiniLabel);
            Action edit = null;
            tree.BeginDraw(true);
            try
            {
                var list = tree.GetPropertyAtPath("Config.Modifiers");
                if (list != null)
                    for (int i = 0; i < list.Children.Count; i++) DrawCard(list.Children[i], i, ref edit, changed);
            }
            finally { tree.EndDraw(); }
            if (edit != null) Edit(edit, changed);
            if (GUILayout.Button("＋ 添加 Modifier")) ChooseType(type =>
                Edit(() => draft.Config.Modifiers.Add(Create(type)), changed));
            if (before != draft.Config.CanonicalIdentity()) changed?.Invoke();
        }

        private void DrawCard(InspectorProperty property, int index, ref Action edit, Action changed)
        {
            var item = property.ValueEntry.WeakSmartValue as ITerrainGenerationModifierConfig;
            bool enabled = item?.Enabled == true;
            Color accent = enabled ? new Color(.34f, .82f, .63f) : new Color(.60f, .61f, .64f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var header = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(header, enabled ? new Color(.12f, .28f, .23f, .55f) : new Color(.2f, .2f, .2f, .3f));
            EditorGUI.DrawRect(new Rect(header.x, header.y, 3, header.height), accent);
            property.State.Expanded = EditorGUI.Foldout(new Rect(header.x + 14, header.y + 5, header.width - 108, 20),
                property.State.Expanded, (index + 1).ToString("00") + "  " + Label(item?.GetType()), true);
            var toggle = property.Children["Enabled"];
            Color original = GUI.backgroundColor;
            GUI.backgroundColor = accent;
            using (new EditorGUI.DisabledScope(toggle?.ValueEntry == null))
            {
                bool next = GUI.Toggle(new Rect(header.xMax - 88, header.y + 4, 84, 21), enabled,
                    enabled ? "✓ 已启用" : "— 已停用", EditorStyles.miniButton);
                if (next != enabled) toggle.ValueEntry.WeakSmartValue = next;
            }
            GUI.backgroundColor = original;
            if (property.State.Expanded)
            {
                EditorGUILayout.LabelField(item?.GetType().Name ?? "缺失类型，请选择或移除", EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUI.DisabledScope(!enabled))
                    foreach (var child in property.Children)
                        if (child.Name != "Enabled") child.Draw();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("更换类型", EditorStyles.miniButton)) ChooseType(type =>
                    Edit(() => draft.Config.Modifiers[index] = Create(type), changed));
                using (new EditorGUI.DisabledScope(index == 0))
                    if (GUILayout.Button("↑", EditorStyles.miniButton, GUILayout.Width(28))) edit = () => Move(index, -1);
                using (new EditorGUI.DisabledScope(index == draft.Config.Modifiers.Count - 1))
                    if (GUILayout.Button("↓", EditorStyles.miniButton, GUILayout.Width(28))) edit = () => Move(index, 1);
                if (GUILayout.Button("移除", EditorStyles.miniButton, GUILayout.Width(42)))
                    edit = () => draft.Config.Modifiers.RemoveAt(index);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private static string Label(Type type) => type == typeof(EntranceWalkwayModifierConfig) ? "入口下洞步道" :
            type == null ? "缺失 Modifier" : ObjectNames.NicifyVariableName(type.Name.Replace("ModifierConfig", ""));

        private static ITerrainGenerationModifierConfig Create(Type type) =>
            (ITerrainGenerationModifierConfig)Activator.CreateInstance(type);

        private static void ChooseType(Action<Type> selected)
        {
            var menu = new GenericMenu();
            foreach (var type in TypeCache.GetTypesDerivedFrom<ITerrainGenerationModifierConfig>()
                .Where(t => !t.IsAbstract && !t.IsGenericType && t.IsSerializable && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.FullName))
            {
                Type choice = type;
                menu.AddItem(new GUIContent(Label(choice) + " (" + choice.Name + ")"), false, () => selected(choice));
            }
            menu.ShowAsContext();
        }

        private void Move(int index, int direction)
        {
            var items = draft.Config.Modifiers;
            var value = items[index]; items.RemoveAt(index); items.Insert(index + direction, value);
        }

        private void Edit(Action edit, Action changed)
        {
            Undo.RecordObject(draft, "修改地形 Modifier 草稿");
            edit(); EditorUtility.SetDirty(draft); Rebind(); changed?.Invoke();
        }

        public void Dispose()
        {
            tree?.Dispose(); tree = null;
            serialized?.Dispose(); serialized = null;
        }
    }
}
