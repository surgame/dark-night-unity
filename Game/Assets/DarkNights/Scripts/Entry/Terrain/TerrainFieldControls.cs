using System;
using System.Collections.Generic;
using System.Reflection;
using DarkNights.View.Terrain;
using UnityEngine;

namespace DarkNights.Entry.Terrain
{
    /// <summary>工作台样式字段控件；只编辑草稿的标量参数，遵守原 Range 与中文字段名，每个参数可恢复应用基线。</summary>
    public static class TerrainFieldControls
    {
        private static readonly Dictionary<Type, FieldInfo[]> Fields = new Dictionary<Type, FieldInfo[]>();
        public static void Draw(CaveStyleDraft draft, ScriptableObject source, Action changed, Func<string, bool> include = null)
        {
            if (source == null) { GUILayout.Label("此层没有配置资产。"); return; }
            var working = draft.Get(source);
            if (!Fields.TryGetValue(source.GetType(), out var fields))
            { fields = source.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public); Fields.Add(source.GetType(), fields); }
            foreach (var field in fields)
            {
                if (include != null && !include(field.Name)) continue;
                var type = field.FieldType;
                if (type != typeof(bool) && type != typeof(int) && type != typeof(float) && type != typeof(string) && !type.IsEnum) continue;
                if (field.Name == "ContentHash") continue;
                object value = field.GetValue(working), next = value;
                string label = field.GetCustomAttribute<InspectorNameAttribute>()?.displayName ?? Label(field.Name);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.ExpandWidth(true));
                bool enabled = GUI.enabled; GUI.enabled = enabled && draft.FieldChanged(source, field);
                if (GUILayout.Button(new GUIContent("↺", "恢复到上次应用的值"), GUILayout.Width(28)))
                { draft.ResetField(source, field); changed(); }
                GUI.enabled = enabled; GUILayout.EndHorizontal();
                GUI.SetNextControlName("TunerField-" + source.GetInstanceID() + "-" + field.Name);
                if (type == typeof(bool)) next = GUILayout.Toggle((bool)value, (bool)value ? "开启" : "关闭");
                else if (type == typeof(string)) next = GUILayout.TextField((string)value ?? "", 80);
                else if (type.IsEnum)
                {
                    var values = Enum.GetValues(type); int current = Array.IndexOf(values, value);
                    next = values.GetValue(GUILayout.SelectionGrid(Mathf.Max(0, current), Enum.GetNames(type), 2));
                }
                else
                {
                    var range = field.GetCustomAttribute<RangeAttribute>();
                    float number = Convert.ToSingle(value);
                    GUILayout.Label(number.ToString(type == typeof(int) ? "0" : "0.##"));
                    if (range != null) number = GUILayout.HorizontalSlider(number, range.min, range.max);
                    next = type == typeof(int) ? (object)Mathf.RoundToInt(number) : number;
                }
                if (!Equals(next, value)) { field.SetValue(working, next); changed(); }
                GUILayout.EndVertical();
            }
        }
        private static string Label(string name)
        {
            switch (name)
            {
                case "Seed": return "独立种子（留空跟随地图）";
                case "ProceduralRock": return "程序岩壁";
                case "ImmediateForeground": return "局部前景刷新";
                case "InteractiveBakeBudgetMs": return "单帧交互预算（毫秒）";
                case "StoneSize": return "岩块尺寸（像素）";
                case "OutlineMode": return "外轮廓方案";
                case "OutlineSeed": return "外轮廓种子";
                case "OutlineAmplitude": return "轮廓幅度（像素）";
                case "OutlineWavelength": return "轮廓波长（像素）";
                case "OutlineQuantization": return "轮廓阶梯（像素）";
                case "ContourStatic": return "三层背景";
                case "MiddleSoftness": return "中层柔边（像素）";
                case "Near": return "显示近层";
                case "Middle": return "显示中层";
                case "Deep": return "显示深层";
                case "AlgorithmVersion": return "源轮廓算法";
            }
            if (name.EndsWith("Amount")) return "点缀密度（%）";
            if (name.EndsWith("Width")) return "点缀范围（格）";
            if (name.EndsWith("Blend")) return "点缀贴边（%）";
            return name;
        }
        public static float Slider(string label, float value, float min, float max)
        { GUILayout.Label(label + "  " + value.ToString("0.##")); return GUILayout.HorizontalSlider(value, min, max); }
    }
}
