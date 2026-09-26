using System;
using System.Collections.Generic;
using DarkNights.Core.Logic.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>编辑器窗口的临时资产草稿；逐字段对照打开时的基线，显式 Apply 才写入原资产，Cancel 直接丢弃。</summary>
    public sealed class TerrainStyleDrafts : IDisposable
    {
        private readonly Dictionary<ScriptableObject, (ScriptableObject Draft, ScriptableObject Baseline)> entries =
            new Dictionary<ScriptableObject, (ScriptableObject Draft, ScriptableObject Baseline)>();

        public T Draft<T>(T source) where T : ScriptableObject
        {
            if (source == null) return null;
            if (!entries.TryGetValue(source, out var entry))
            {
                entry = (UnityEngine.Object.Instantiate(source), UnityEngine.Object.Instantiate(source));
                entry.Draft.hideFlags = HideFlags.DontSave;
                entry.Baseline.hideFlags = HideFlags.DontSave;
                entries.Add(source, entry);
            }
            return (T)entry.Draft;
        }

        public bool HasChanges
        {
            get
            {
                foreach (var entry in entries.Values)
                    if (ChangedPaths(entry).Count != 0) return true;
                return false;
            }
        }

        public ScriptableObject ChooseAsset(CaveTerrainStyle style, ScriptableObject selected)
        {
            var assets = new List<ScriptableObject>();
            var labels = new List<string>();
            void Add(string label, ScriptableObject asset)
            { if (asset != null) { labels.Add(label); assets.Add(asset); } }
            void AddModifiers(string layer, CaveModifierAsset[] modifiers)
            {
                if (modifiers == null) return;
                for (int i = 0; i < modifiers.Length; i++)
                    Add(layer + " / " + (i + 1) + " / " + (modifiers[i] != null ? modifiers[i].name : "空"), modifiers[i]);
            }
            Add("岩壁 / 外轮廓", style);
            if (style != null)
            {
                var working = Draft(style);
                AddModifiers("前景", working.Modifiers);
                var background = working.Background;
                Add("三层背景", background);
                if (background != null)
                {
                    var layer = Draft(background);
                    Add("点缀生成器", layer.Generator);
                    AddModifiers("近层", layer.NearModifiers);
                    AddModifiers("中层", layer.MiddleModifiers);
                    AddModifiers("深层", layer.DeepModifiers);
                }
            }
            if (assets.Count == 0) return null;
            int index = assets.IndexOf(selected);
            int next = EditorGUILayout.Popup("资产", Mathf.Max(0, index), labels.ToArray());
            return assets[Mathf.Clamp(next, 0, assets.Count - 1)];
        }

        public CaveModifierStack Capture(CaveModifierAsset[] sources)
        {
            if (sources == null) return CaveModifierStack.Empty;
            var working = new CaveModifierAsset[sources.Length];
            for (int i = 0; i < sources.Length; i++) working[i] = Draft(sources[i]);
            return CaveModifierAsset.CaptureStack(working);
        }

        public bool Reset(ScriptableObject source, string path)
        {
            if (!entries.TryGetValue(source, out var entry)) return false;
            var data = new SerializedObject(entry.Draft);
            var baseline = new SerializedObject(entry.Baseline);
            var current = data.FindProperty(path);
            var original = baseline.FindProperty(path);
            if (current == null || original == null || SerializedProperty.DataEquals(current, original)) return false;
            data.CopyFromSerializedProperty(original);
            data.ApplyModifiedProperties();
            return true;
        }

        public void DrawInspector(ScriptableObject source, Action onChange)
        {
            var draft = Draft(source);
            if (draft == null) return;
            var baseline = new SerializedObject(entries[source].Baseline);
            var data = new SerializedObject(draft);
            data.Update(); baseline.Update();
            var field = data.GetIterator();
            bool changed = false;
            for (bool next = field.NextVisible(true); next; next = field.NextVisible(false))
            {
                if (field.propertyPath == "m_Script") continue;
                var original = baseline.FindProperty(field.propertyPath);
                bool modified = original != null && !SerializedProperty.DataEquals(field, original);
                float height = EditorGUI.GetPropertyHeight(field, true);
                var row = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));
                if (modified) row.width -= 26;
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(row, field, true);
                changed |= EditorGUI.EndChangeCheck();
                if (modified && GUI.Button(new Rect(row.xMax + 2, row.y, 24, EditorGUIUtility.singleLineHeight),
                    new GUIContent("↺", "恢复此参数在打开草稿时的原资产值")))
                {
                    data.ApplyModifiedProperties();
                    Reset(source, field.propertyPath);
                    changed = true;
                }
            }
            if (changed)
            {
                data.ApplyModifiedProperties();
                onChange();
            }
        }

        public int Apply()
        {
            var changes = new Dictionary<ScriptableObject, List<string>>();
            foreach (var pair in entries)
            {
                var paths = ChangedPaths(pair.Value);
                if (paths.Count == 0) continue;
                var current = new SerializedObject(pair.Key);
                var baseline = new SerializedObject(pair.Value.Baseline);
                foreach (string path in paths)
                    if (!SerializedProperty.DataEquals(current.FindProperty(path), baseline.FindProperty(path)))
                        throw new InvalidOperationException(pair.Key.name + " 的 " + path + " 已在窗口外变化；请先 Cancel 并重新打开草稿。");
                changes.Add(pair.Key, paths);
            }
            if (changes.Count == 0) return 0;
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Cave Wall Tuner");
            var targets = new UnityEngine.Object[changes.Count];
            int index = 0;
            foreach (var source in changes.Keys) targets[index++] = source;
            Undo.RecordObjects(targets, "Apply Cave Wall Tuner");
            foreach (var pair in changes)
            {
                var target = new SerializedObject(pair.Key);
                var draft = new SerializedObject(entries[pair.Key].Draft);
                foreach (string path in pair.Value)
                    target.CopyFromSerializedProperty(draft.FindProperty(path));
                target.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pair.Key);
            }
            foreach (var source in changes.Keys) AssetDatabase.SaveAssetIfDirty(source);
            Undo.CollapseUndoOperations(group);
            return changes.Count;
        }

        /// <summary>接收运行工作台草稿及最初源基线；沿用逐字段冲突校验，运行时的 Apply 不会绕过外部资产保护。</summary>
        public void Import(CaveStyleDraft source)
        {
            Clear();
            foreach (var item in source.Items)
            {
                var working = UnityEngine.Object.Instantiate(item.Working);
                var baseline = UnityEngine.Object.Instantiate(item.Original);
                working.hideFlags = baseline.hideFlags = HideFlags.DontSave;
                entries.Add(item.Source, (working, baseline));
            }
        }

        private static List<string> ChangedPaths((ScriptableObject Draft, ScriptableObject Baseline) entry)
        {
            var draft = new SerializedObject(entry.Draft);
            var baseline = new SerializedObject(entry.Baseline);
            var paths = new List<string>();
            var field = draft.GetIterator();
            for (bool next = field.NextVisible(true); next; next = field.NextVisible(false))
            {
                if (field.propertyPath == "m_Script") continue;
                var original = baseline.FindProperty(field.propertyPath);
                if (original != null && !SerializedProperty.DataEquals(field, original))
                    paths.Add(field.propertyPath);
            }
            return paths;
        }

        public void Clear()
        {
            foreach (var entry in entries.Values)
            {
                UnityEngine.Object.DestroyImmediate(entry.Draft);
                UnityEngine.Object.DestroyImmediate(entry.Baseline);
            }
            entries.Clear();
        }

        public void Dispose() => Clear();
    }
}
