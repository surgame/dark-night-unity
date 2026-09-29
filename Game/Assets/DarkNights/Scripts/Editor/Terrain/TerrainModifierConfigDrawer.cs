using System;
using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace DarkNights.Editor.Terrain
{
    /// <summary>共用 YYGC Objects 的 Odin PropertyTree 绘制方式，直接选择 SerializeReference 实现并编辑其字段。</summary>
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
            EditorGUI.BeginChangeCheck();
            tree.BeginDraw(true);
            tree.GetPropertyAtPath("Config.Modifiers")?.Draw();
            tree.EndDraw();
            if (EditorGUI.EndChangeCheck()) changed?.Invoke();
        }

        public void Dispose()
        {
            tree?.Dispose(); tree = null;
            serialized?.Dispose(); serialized = null;
        }
    }
}
