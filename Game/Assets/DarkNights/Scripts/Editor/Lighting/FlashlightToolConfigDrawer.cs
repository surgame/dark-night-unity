using System;
using System.Linq;
using DarkNights.Runtime.Objects;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>现有 Definition 工坊的照明能力抽屉；选择预设并逐项覆盖，直接编辑原 Definition，兼容工坊原有保存和原生 Undo。</summary>
    public sealed class FlashlightToolConfigDrawer : OdinAttributeDrawer<LightConfigurationAttribute, LightOverrides>
    {
        private readonly FlashlightDebugUndo undo = new FlashlightDebugUndo();
        private int dragControl;
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var owner = Owner;
            if (owner == null)
            {
                EditorGUILayout.HelpBox("照明面板未取得当前 Definition，无法编辑配置。", MessageType.Error); return;
            }
            try
            {
                var current = LightDefinitionEditor.Config(owner);
                if (current == null) return;
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    undo.BeginGesture("config"); dragControl = 0;
                }
                var candidate = LightDefinitionEditor.Copy(current);
                candidate.Starter = EditorGUILayout.Toggle("新角色配发", candidate.Starter);
                var preset = LightDefinitionEditor.Preset(owner);
                var selected = (LightProfile)EditorGUILayout.ObjectField("光照预设（可选）", preset, typeof(LightProfile), false);
                if (selected != preset)
                {
                    LightDefinitionEditor.SelectPreset(owner, selected, undo); Synchronize(); return;
                }
                EditorGUILayout.HelpBox(preset != null
                    ? "直接修改参数即可覆盖；蓝点表示物体覆盖，其他项跟随预设。点蓝点或右键可恢复跟随预设。"
                    : "未引用预设：使用内置默认光照。直接修改即可覆盖；点蓝点或右键可恢复默认值。", MessageType.None);
                foreach (var field in LightParameterSchema.Fields)
                {
                    var mask = LightParameterSchema.Mask(field.Name);
                    bool overridden = candidate.Overrides.Has(mask);
                    var row = EditorGUILayout.GetControlRect();
                    DrawContextMenu(row, owner, field.Name, overridden, preset != null);
                    var indicator = new Rect(row.x, row.y, 18, row.height);
                    if (DrawIndicator(indicator, overridden, preset != null))
                    {
                        candidate.Overrides.Mask &= ~mask;
                        overridden = false;
                    }
                    var member = typeof(LightOverrides).GetField(field.Name);
                    object value = overridden ? member.GetValue(candidate.Overrides) : LightDefinitionEditor.InheritedValue(preset, field.Name);
                    var content = new GUIContent(field.Label, overridden ? "已覆盖：仅修改当前物体。" : "跟随来源；直接修改即可覆盖当前物体。");
                    var control = new Rect(row.x + 18, row.y, row.width - 18, row.height);
                    using (var change = new EditorGUI.ChangeCheckScope())
                    {
                        object edited;
                        if (member.FieldType == typeof(float)) edited = EditorGUI.Slider(control, content, (float)value, field.Low, field.High);
                        else if (member.FieldType == typeof(bool)) edited = EditorGUI.Toggle(control, content, (bool)value);
                        else edited = EditorGUI.ColorField(control, content, (Color)value);
                        if (change.changed && !Equals(value, edited))
                        {
                            member.SetValue(candidate.Overrides, edited);
                            candidate.Overrides.Mask |= mask;
                        }
                    }
                }
                if (LightDefinitionEditor.Apply(owner, candidate, undo)) Synchronize();
                if (GUILayout.Button("打开光照配置调试台")) FlashlightDebugWindow.Open(owner);
            }
            catch (ExitGUIException) { throw; }
            catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
            finally
            {
                var currentEvent = Event.current;
                if (currentEvent != null && undo.IsGestureActive)
                {
                    int captured = GUIUtility.hotControl;
                    bool released = currentEvent.rawType == EventType.MouseUp && currentEvent.button == 0;
                    bool lostCapture = dragControl != 0 && captured != dragControl;
                    bool ignoredWithoutCapture = currentEvent.rawType == EventType.Ignore && captured == 0;
                    if (released || lostCapture || ignoredWithoutCapture)
                    {
                        dragControl = 0;
                        Property.Tree.DelayAction(undo.EndGesture);
                    }
                    else if (captured != 0) dragControl = captured;
                }
            }
        }

        private static bool DrawIndicator(Rect rect, bool overridden, bool hasPreset)
        {
            bool reset;
            using (new EditorGUI.DisabledScope(!overridden))
                reset = GUI.Button(rect, new GUIContent("", overridden ? "已覆盖；点击恢复" + (hasPreset ? "跟随预设。" : "内置默认值。") : "跟随来源。"), GUIStyle.none);
            if (overridden && Event.current.type == EventType.Repaint)
            {
                Handles.BeginGUI();
                var previous = Handles.color;
                Handles.color = new Color(.25f, .6f, 1f);
                Handles.DrawSolidDisc(rect.center, Vector3.forward, 3f);
                Handles.color = previous;
                Handles.EndGUI();
            }
            return reset;
        }

        private void DrawContextMenu(Rect row, ObjectDefinition owner, string field, bool overridden, bool hasPreset)
        {
            if (Event.current.type != EventType.ContextClick || !row.Contains(Event.current.mousePosition)) return;
            var menu = new GenericMenu();
            var content = new GUIContent(hasPreset ? "恢复跟随预设" : "恢复内置默认值");
            if (overridden) menu.AddItem(content, false, () =>
            {
                undo.EndGesture();
                LightDefinitionEditor.EnableOverride(owner, field, false, undo);
                if (Owner == owner) Synchronize();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            });
            else menu.AddDisabledItem(content);
            menu.ShowAsContext(); Event.current.Use();
        }

        private void Synchronize()
        {
            var owner = Owner;
            var config = LightDefinitionEditor.Config(owner);
            Property.Parent.ValueEntry.WeakSmartValue = config;
            ValueEntry.SmartValue = config.Overrides;
        }

        private ObjectDefinition Owner => Property.Tree.WeakTargets.OfType<ObjectDefinition>().FirstOrDefault()
            ?? Property.Tree.UnitySerializedObject?.targetObject as ObjectDefinition;
    }
}
