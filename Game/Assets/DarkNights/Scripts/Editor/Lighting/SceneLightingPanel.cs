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
            var mode = new EnumField("墙面返光模式", draft.Settings.BounceMode) { name = "BounceMode" };
            mode.RegisterValueChangedCallback(evt => Write("BounceMode", (LightBounceMode)evt.newValue));
            Root.Add(mode); synchronize.Add(() => mode.SetValueWithoutNotify(draft.Settings.BounceMode));
            Root.Add(capability); synchronize.Add(() => capability.text = draft.Settings.Backend == LightingBackendKind.Urp2D ?
                "URP 2D 保留原行为，不支持墙内限深及墙段连续漫反射。" :
                draft.Settings.DiffuseShape == WallDiffuseShape.SoftBlob ? "柔光团：最大范围控制半径，沿墙扩展仅用于 WallBand；旧反射强度仅用于 LegacyPoint。" :
                "沿墙扩散：相邻受光墙面圆滑连接；旧反射强度仅用于 LegacyPoint。");
            foreach (string name in FlashlightDebugSave.SceneFields.Skip(1))
            {
                if (name == "BounceMode") continue;
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
            foreach (var item in new[] { ("明亮", .32f), ("平衡", .24f), ("偏暗", .1f), ("零环境光", 0f) })
                Root.Add(new Button(() => Write("Ambient", item.Item2)) { text = item.Item1 });
        }

        private void Write(string name, object value)
        {
            var member = typeof(ExplorationLightSettings).GetField(name);
            if (Equals(member.GetValue(draft.Settings), value)) return;
            var candidate = JsonUtility.FromJson<ExplorationLightSettings>(JsonUtility.ToJson(draft.Settings));
            member.SetValue(candidate, value);
            try { candidate.Validate(); }
            catch (ArgumentException) { capability.text = "距离必须满足远端大于近端；参数须在合法范围内。"; return; }
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
                case "Bounce": return "有限反射补光";
                case "DiffuseGain": return "墙面漫反射增益";
                case "DiffuseShape": return "漫反射形状（WallBand 沿墙／SoftBlob 柔光团）";
                case "DiffuseMatchIncident": return "匹配主光入射亮度（不额外按斜照压暗）";
                case "DiffuseCornerRadius": return "相邻墙面连接半径（格，0关闭）";
                case "DiffuseNearDistance": return "漫反射近距离（格）";
                case "DiffuseFarDistance": return "漫反射关闭距离（格）";
                case "DiffuseMaxReach": return "漫反射最大范围／法向深度（格）";
                case "DiffuseLateralSpread": return "漫反射沿墙扩展";
                case "DiffuseVisibilityProbes": return "每墙段遮挡探针";
                default: return "岩壁受光层次";
            }
        }
    }
}
