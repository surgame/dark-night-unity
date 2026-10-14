using System;
using System.Collections.Generic;
using DarkNights.Core.Config;
using DarkNights.Runtime.Objects;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Lighting
{
    /// <summary>调试台的逐项预设与覆盖控件；每次编辑读取 Definition 最新配置，刷新无通知，Undo 恢复后不保留过期托管引用。</summary>
    public sealed class LightOverridePanel
    {
        private readonly ObjectDefinition owner;
        private readonly LightProfile preset;
        private readonly bool editPreset;
        private readonly FlashlightDebugUndo undo;
        private readonly FlashlightDebugDraft draft;
        private readonly List<Action<LightBounceMode, WallDiffuseShape, WallDiffuseOcclusionMode>> refresh =
            new List<Action<LightBounceMode, WallDiffuseShape, WallDiffuseOcclusionMode>>();
        private readonly Label feedback = new Label();
        private readonly bool categorized;
        private readonly Dictionary<string, VisualElement> groups = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, VisualElement> sections = new Dictionary<string, VisualElement>();
        public VisualElement Root { get; } = new VisualElement();
        public string Feedback => feedback.text;
        public VisualElement Group(string name) => groups[name];

        public LightOverridePanel(ObjectDefinition owner, LightProfile preset, bool editPreset, FlashlightDebugUndo undo)
        {
            this.owner = owner; this.preset = preset; this.editPreset = editPreset; this.undo = undo;
            Root.Add(feedback);
            foreach (var field in LightParameterSchema.Fields) Add(field.Name, field.Label, field.Low, field.High);
            Refresh();
        }

        public LightOverridePanel(FlashlightDebugDraft draft, FlashlightDebugUndo undo, bool categorized = false)
        {
            this.draft = draft; this.undo = undo; preset = draft.Preset; this.categorized = categorized;
            Root.Add(feedback);
            foreach (var field in LightParameterSchema.Fields) Add(field.Name, field.Label, field.Low, field.High);
            Refresh();
        }

        private bool Overridden(string name) => draft != null ? draft.Values.Has(LightParameterSchema.Mask(name))
            : LightDefinitionEditor.Config(owner)?.Overrides.Has(LightParameterSchema.Mask(name)) == true;

        public void Refresh()
        {
            var mode = (LightBounceMode)Read("BounceMode");
            var shape = (WallDiffuseShape)Read("DiffuseShape");
            var occlusion = (WallDiffuseOcclusionMode)Read("DiffuseOcclusionMode");
            foreach (var update in refresh) update(mode, shape, occlusion);
        }

        private object Read(string name)
        {
            if (draft != null) return draft.Read(name);
            var config = LightDefinitionEditor.Config(owner);
            if (!editPreset && config != null && config.Overrides.Has(LightParameterSchema.Mask(name)))
                return typeof(LightOverrides).GetField(name).GetValue(config.Overrides);
            return LightDefinitionEditor.InheritedValue(preset, name);
        }

        private void Write(string name, object value)
        {
            try
            {
                if (Equals(Read(name), value)) return;
                if (draft != null) draft.Write(name, value, undo);
                else if (editPreset)
                {
                    var candidate = UnityEngine.Object.Instantiate(preset);
                    try { typeof(LightProfile).GetField(name).SetValue(candidate, value); candidate.Freeze(); }
                    finally { UnityEngine.Object.DestroyImmediate(candidate); }
                    undo.Change(preset, name, "修改光照预设", () => typeof(LightProfile).GetField(name).SetValue(preset, value));
                }
                else LightDefinitionEditor.SetValue(owner, name, value, undo);
                feedback.text = "";
            }
            catch (ArgumentException error) { feedback.text = error.Message; }
            Refresh();
        }

        private void Revert(string name)
        {
            undo.EndGesture();
            if (draft != null) draft.Revert(name, undo);
            else LightDefinitionEditor.EnableOverride(owner, name, false, undo);
            Refresh();
        }

        private void Add(string name, string label, float low, float high)
        {
            var row = new VisualElement(); row.AddToClassList("override-row");
            var indicator = new Button(() => Revert(name)) { name = "override" + name };
            indicator.AddToClassList("override-indicator");
            var dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.AddToClassList("override-dot"); indicator.Add(dot);
            if (!editPreset)
            {
                row.Add(indicator);
                row.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    bool overridden = Overridden(name);
                    evt.menu.AppendAction(draft != null ? "恢复持久化配置" : preset != null ? "恢复跟随预设" : "恢复内置默认值", _ => Revert(name),
                        overridden ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                }));
            }
            var type = typeof(LightProfile).GetField(name).FieldType;
            VisualElement field;
            Action synchronize;
            if (type == typeof(float))
            {
                var slider = new Slider(label, low, high) { name = name, showInputField = true };
                var input = slider.Q<TextField>(); if (input != null) input.isDelayed = true;
                slider.RegisterValueChangedCallback(evt => Write(name, evt.newValue));
                slider.AddManipulator(new FlashlightDebugSliderGesture(() => undo.BeginGesture(name), undo.EndGesture));
                field = slider;
                synchronize = () => { float value = (float)Read(name); if (slider.value != value) slider.SetValueWithoutNotify(value); };
            }
            else if (type == typeof(WallDiffuseOcclusionMode))
            {
                var choice = new PopupField<string>(label, new List<string> { "原始切边", "墙体遮罩", "完整返光羽化" }, (int)(WallDiffuseOcclusionMode)Read(name)) { name = name };
                choice.RegisterValueChangedCallback(evt => Write(name, (WallDiffuseOcclusionMode)choice.index));
                field = choice;
                synchronize = () => choice.SetValueWithoutNotify(choice.choices[(int)(WallDiffuseOcclusionMode)Read(name)]);
            }
            else if (type.IsEnum)
            {
                var choice = new EnumField(label, (Enum)Read(name)) { name = name };
                choice.RegisterValueChangedCallback(evt => Write(name, evt.newValue)); field = choice;
                synchronize = () => choice.SetValueWithoutNotify((Enum)Read(name));
            }
            else if (type == typeof(int))
            {
                var slider = new SliderInt(label, (int)low, (int)high) { name = name, showInputField = true };
                slider.RegisterValueChangedCallback(evt => Write(name, evt.newValue));
                slider.AddManipulator(new FlashlightDebugSliderGesture(() => undo.BeginGesture(name), undo.EndGesture));
                field = slider; synchronize = () => slider.SetValueWithoutNotify((int)Read(name));
            }
            else if (type == typeof(bool))
            {
                var toggle = new Toggle(label) { name = name };
                toggle.RegisterValueChangedCallback(evt => Write(name, evt.newValue)); field = toggle;
                synchronize = () => toggle.SetValueWithoutNotify((bool)Read(name));
            }
            else
            {
                var color = new ColorField(label) { name = name, hdr = false, showAlpha = false };
                color.RegisterValueChangedCallback(evt => Write(name, evt.newValue)); field = color;
                synchronize = () => color.SetValueWithoutNotify((Color)Read(name));
            }
            field.AddToClassList("override-value"); row.Add(field); Parent(name).Add(row);
            refresh.Add((mode, shape, occlusion) =>
            {
                row.EnableInClassList("is-hidden", !LightParameterSchema.IsVisible(name, mode, shape, occlusion));
                var caption = field.Q<Label>(className: "unity-base-field__label");
                if (caption != null) caption.text = LightParameterSchema.Label(name, label, shape);
                bool overridden = Overridden(name);
                row.EnableInClassList("is-overridden", !editPreset && overridden);
                indicator.SetEnabled(overridden);
                indicator.tooltip = overridden ? draft != null ? "运行时临时覆盖；点击恢复持久化配置。" : "已覆盖；点击恢复" + (preset != null ? "跟随预设。" : "内置默认值。") : "跟随来源。";
                field.tooltip = (draft != null ? "修改运行时副本；确认保存前不会写入资产。" : editPreset ? "编辑共享预设，影响所有未覆盖此项的物体。"
                    : overridden ? "已覆盖：仅修改当前物体。" : "跟随来源；直接修改即可覆盖当前物体。") + LightParameterSchema.Hint(name);
                synchronize();
            });
        }

        private VisualElement Parent(string name)
        {
            if (!categorized) return Root;
            string group = LightParameterSchema.Group(name), section = LightParameterSchema.Section(name), key = group + section;
            if (!groups.TryGetValue(group, out var parent)) { parent = new VisualElement(); groups.Add(group, parent); Root.Add(parent); }
            if (sections.TryGetValue(key, out var found)) return found;
            VisualElement container;
            if (section == "高级采样") container = new Foldout { text = section, value = false };
            else { container = new VisualElement(); var title = new Label(section); title.AddToClassList("light-group-heading"); container.Add(title); }
            container.AddToClassList("light-parameter-section"); parent.Add(container); sections.Add(key, container); return container;
        }
    }
}
