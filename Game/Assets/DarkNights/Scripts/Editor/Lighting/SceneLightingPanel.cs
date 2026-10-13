using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.View.Lighting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Lighting
{
    /// <summary>场景光照副本的调参面板；复用原参数类型及 Range，编辑前校验候选并登记临时历史，刷新不写资产或生成历史。</summary>
    public sealed class SceneLightingPanel
    {
        private readonly SceneLightingProfile draft;
        private readonly FlashlightDebugUndo undo;
        private readonly Action changed;
        private readonly List<Action> synchronize = new List<Action>();
        private readonly Label capability = new Label();
        public VisualElement Root { get; } = new VisualElement();

        public SceneLightingPanel(SceneLightingProfile profile, FlashlightDebugUndo undo, Action changed)
        {
            if (profile == null || EditorUtility.IsPersistent(profile)) throw new ArgumentException("场景调试只接受临时副本。");
            draft = profile; this.undo = undo; this.changed = changed;
            var backend = new EnumField("渲染后端", draft.Settings.Backend) { name = "Backend" };
            backend.RegisterValueChangedCallback(evt => Write("Backend", (LightingBackendKind)evt.newValue));
            Root.Add(backend); synchronize.Add(() => backend.SetValueWithoutNotify(draft.Settings.Backend));
            Root.Add(capability); synchronize.Add(() => capability.text = draft.Settings.Backend == LightingBackendKind.Urp2D ?
                "URP 2D 不支持墙内限深与单灯连续漫反射；返光参数在手电配置中编辑。" :
                "场景只控制共享墙体受光规则；返光模式和光形在手电配置中编辑。");
            foreach (string name in FlashlightDebugSave.SceneFields.Skip(1))
            {
                var member = typeof(ExplorationLightSettings).GetField(name);
                if (member.FieldType.IsEnum)
                {
                    var shape = new EnumField(Label(name), (Enum)member.GetValue(draft.Settings)) { name = name };
                    shape.RegisterValueChangedCallback(evt => Write(name, evt.newValue));
                    Root.Add(shape); synchronize.Add(() => shape.SetValueWithoutNotify((Enum)member.GetValue(draft.Settings))); continue;
                }
                if (member.FieldType == typeof(bool))
                {
                    var match = new Toggle(Label(name)) { name = name };
                    match.RegisterValueChangedCallback(evt => Write(name, evt.newValue));
                    Root.Add(match); synchronize.Add(() => match.SetValueWithoutNotify((bool)member.GetValue(draft.Settings))); continue;
                }
                var range = (RangeAttribute)member.GetCustomAttributes(typeof(RangeAttribute), false).Single();
                if (member.FieldType == typeof(int))
                {
                    var probes = new SliderInt(Label(name), (int)range.min, (int)range.max) { name = name, showInputField = true };
                    probes.RegisterValueChangedCallback(evt => Write(name, evt.newValue));
                    probes.AddManipulator(new FlashlightDebugSliderGesture(() => undo.BeginGesture("scene" + name), undo.EndGesture));
                    Root.Add(probes); synchronize.Add(() => probes.SetValueWithoutNotify((int)member.GetValue(draft.Settings))); continue;
                }
                var slider = new Slider(Label(name), range.min, range.max) { name = name, showInputField = true };
                var input = slider.Q<TextField>(); if (input != null) input.isDelayed = true;
                slider.RegisterValueChangedCallback(evt => Write(name, evt.newValue));
                slider.AddManipulator(new FlashlightDebugSliderGesture(() => undo.BeginGesture("scene" + name), undo.EndGesture));
                Root.Add(slider); synchronize.Add(() => slider.SetValueWithoutNotify((float)member.GetValue(draft.Settings)));
            }
            var ambient = new PopupField<string>("底光快捷值",
                new List<string> { "自定义", "明亮 · 0.32", "平衡 · 0.24", "偏暗 · 0.10", "零底光 · 0" }, 0);
            ambient.RegisterValueChangedCallback(evt =>
            { int index = ambient.choices.IndexOf(evt.newValue); if (index > 0) Write("Ambient", new[] { .32f, .24f, .1f, 0f }[index - 1]); });
            Root.Add(ambient); synchronize.Add(() =>
            {
                int index = Array.FindIndex(new[] { .32f, .24f, .1f, 0f }, value => Mathf.Approximately(value, draft.Settings.Ambient));
                ambient.SetValueWithoutNotify(ambient.choices[index < 0 ? 0 : index + 1]);
            });
        }

        private void Write(string name, object value)
        {
            var member = typeof(ExplorationLightSettings).GetField(name);
            if (Equals(member.GetValue(draft.Settings), value)) return;
            var candidate = JsonUtility.FromJson<ExplorationLightSettings>(JsonUtility.ToJson(draft.Settings));
            member.SetValue(candidate, value);
            try { candidate.Validate(); }
            catch (ArgumentException) { capability.text = "场景参数须在合法范围内。"; return; }
            undo.Change(draft, "scene" + name, "修改场景光照临时参数", () => draft.Settings = candidate);
            changed();
            Refresh();
        }
        public void Refresh() { foreach (var update in synchronize) update(); }
        public void Dispose() { Root.Clear(); synchronize.Clear(); }
        private static string Label(string name)
        {
            switch (name)
            {
                case "Ambient": return "环境底光";
                case "WallDepth": return "墙内最大深度（格）";
                case "WallStrength": return "墙内补光上限";
                default: return "岩壁受光层次";
            }
        }
    }
}
