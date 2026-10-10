using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>调试台临时副本的独立历史；只接收非持久化对象，合并一次拖动并保留最近 128 次修改，撤销到边界后停止，不回退工程资产历史。</summary>
    public sealed class FlashlightDebugHistory
    {
        private readonly List<(UnityEngine.Object Owner, string Before, string After)> entries = new List<(UnityEngine.Object, string, string)>();
        private UnityEngine.Object target;
        private string control;
        private string before;
        private int cursor;
        private const int Capacity = 128;
        public bool CanUndo => cursor > 0;
        public bool CanRedo => cursor < entries.Count;
        public void Begin(string name) { End(); control = name; }
        public void End()
        {
            if (target != null && before != null) Record(target, before, JsonUtility.ToJson(target));
            target = null; control = null; before = null;
        }

        public void Change(UnityEngine.Object owner, string name, Action edit)
        {
            if (owner == null || EditorUtility.IsPersistent(owner)) throw new ArgumentException("临时调参历史只能编辑运行时副本。");
            bool dragging = control == name && (target == null || target == owner);
            if (!dragging) End();
            string old = JsonUtility.ToJson(owner);
            if (dragging && target == null) { target = owner; before = old; }
            edit();
            if (!dragging) Record(owner, old, JsonUtility.ToJson(owner));
        }

        private void Record(UnityEngine.Object owner, string old, string current)
        {
            if (old == current) return;
            if (cursor < entries.Count) entries.RemoveRange(cursor, entries.Count - cursor);
            if (entries.Count == Capacity) entries.RemoveAt(0);
            entries.Add((owner, old, current)); cursor = entries.Count;
        }
        public void Undo()
        {
            End(); if (!CanUndo) return;
            var entry = entries[--cursor];
            if (entry.Owner != null) JsonUtility.FromJsonOverwrite(entry.Before, entry.Owner);
        }
        public void Redo()
        {
            End(); if (!CanRedo) return;
            var entry = entries[cursor++];
            if (entry.Owner != null) JsonUtility.FromJsonOverwrite(entry.After, entry.Owner);
        }
        public void Clear() { target = null; control = null; before = null; entries.Clear(); cursor = 0; }
    }
}
