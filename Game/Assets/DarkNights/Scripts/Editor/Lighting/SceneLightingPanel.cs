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
        public VisualElement Root { get; } = new VisualElement();

        public SceneLightingPanel(SceneLightingProfile profile, FlashlightDebugUndo undo, Action changed)
        {
            if (profile == null || EditorUtility.IsPersistent(profile)) throw new ArgumentException("场景调试只接受临时副本。");
            draft = profile; this.undo = undo; this.changed = changed;
            var backend = new EnumField("渲染后端", draft.Settings.Backend) { name = "Backend" };
            backend.RegisterValueChangedCallback(evt => Write("Backend", (LightingBackendKind)evt.newValue));
            Root.Add(backend); synchronize.Add(() => backend.SetValueWithoutNotify(draft.Settings.Backend));
            foreach (string name in FlashlightDebugSave.SceneFields.Skip(1))
            {
                var member = typeof(ExplorationLightSettings).GetField(name);
                var range = (RangeAttribute)member.GetCustomAttributes(typeof(RangeAttribute), false).Single();
                var slider = new Slider(Label(name), range.min, range.max) { name = name, showInputField = true };
                var input = slider.Q<TextField>(); if (input != null) input.isDelayed = true;
                slider.RegisterValueChangedCallback(evt => Write(name, evt.newValue));
                slider.AddManipulator(new FlashlightDebugSliderGesture(() => undo.BeginGesture("scene" + name), undo.EndGesture));
                Root.Add(slider); synchronize.Add(() => slider.SetValueWithoutNotify((float)member.GetValue(draft.Settings)));
            }
            foreach (var item in new[] { ("明亮", .32f), ("平衡", .24f), ("偏暗", .1f), ("零环境光", 0f) })
                Root.Add(new Button(() => Write("Ambient", item.Item2)) { text = item.Item1 });
        }

        private void Write(string name, object value)
        {
            var member = typeof(ExplorationLightSettings).GetField(name);
            if (Equals(member.GetValue(draft.Settings), value)) return;
            var candidate = JsonUtility.FromJson<ExplorationLightSettings>(JsonUtility.ToJson(draft.Settings));
            member.SetValue(candidate, value); candidate.Validate();
            undo.Change(draft, "scene" + name, "修改场景光照临时参数", () => draft.Settings = candidate);
            changed();
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
                case "Bounce": return "有限反射补光";
                default: return "岩壁受光层次";
            }
        }
    }
}
