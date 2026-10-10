using System;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>照明操作的手势边界；工坊保存起点组并合并原生记录，调试台转交独立临时历史，控件切换先封闭手势，不清除资产历史。</summary>
    public sealed class FlashlightDebugUndo
    {
        private UnityEngine.Object target;
        private string gesture;
        private bool recorded;
        private int group = -1;
        private readonly FlashlightDebugHistory temporary;
        public bool IsGestureActive => gesture != null;
        public bool CanUndo => temporary?.CanUndo == true;
        public bool CanRedo => temporary?.CanRedo == true;
        public FlashlightDebugUndo(FlashlightDebugHistory temporary = null) { this.temporary = temporary; }
        public void UndoTemporary() => temporary?.Undo();
        public void RedoTemporary() => temporary?.Redo();
        public void ClearTemporary() { EndGesture(); temporary?.Clear(); }

        public void BeginGesture(string control)
        {
            EndGesture(); gesture = control; temporary?.Begin(control);
        }

        public void EndGesture()
        {
            if (temporary != null)
            { temporary.End(); gesture = null; recorded = false; target = null; group = -1; return; }
            if (recorded)
            {
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
                Undo.IncrementCurrentGroup();
            }
            gesture = null; recorded = false; target = null; group = -1;
        }

        public void Change(UnityEngine.Object owner, string control, string label, Action edit)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (temporary != null) { temporary.Change(owner, control, edit); return; }
            bool dragging = gesture == control && (target == null || target == owner);
            if (!dragging && IsGestureActive) EndGesture();
            if (!dragging || !recorded)
            {
                Undo.IncrementCurrentGroup();
                if (dragging) group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(label);
                Undo.RegisterCompleteObjectUndo(owner, label);
                if (dragging) { recorded = true; target = owner; }
                else Undo.IncrementCurrentGroup();
            }
            edit(); EditorUtility.SetDirty(owner); LightingEditorRefresh.Request();
        }
    }
}
