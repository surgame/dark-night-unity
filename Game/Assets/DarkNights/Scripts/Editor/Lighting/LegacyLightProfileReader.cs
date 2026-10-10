using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DarkNights.View.Lighting;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>一次性迁移的旧作者值读取器；只读保全的原生文本，按组件身份提取参数并叠加已知覆盖，不反写 YAML 或重建资源身份。</summary>
    public static class LegacyLightProfileReader
    {
        public static void Read(LightProfile destination, string baseText, string overridesText = null)
        {
            string environment = Document(baseText, "LightEnvironmentEmitter");
            string fill = Document(baseText, "LocalLightFill");
            destination.EnvironmentEnabled = Number(environment, "m_Enabled") != 0;
            destination.LocalFillEnabled = Number(fill, "m_Enabled") != 0;
            destination.Directional = Number(environment, "directional") != 0;
            destination.Range = Number(environment, "range"); destination.Cone = Number(environment, "cone");
            destination.ApertureWidth = Number(environment, "apertureWidth"); destination.Intensity = Number(environment, "intensity");
            destination.NearRange = Number(environment, "nearRange"); destination.NearIntensity = Number(environment, "nearIntensity");
            destination.Color = ColorValue(environment); destination.FillColor = ColorValue(fill);
            destination.FillRadius = Number(fill, "radius"); destination.FillIntensity = Number(fill, "intensity");
            if (string.IsNullOrEmpty(overridesText)) return;
            string environmentId = Regex.Match(environment, @"&([0-9]+)").Groups[1].Value;
            string fillId = Regex.Match(fill, @"&([0-9]+)").Groups[1].Value;
            foreach (Match item in Regex.Matches(overridesText,
                @"target: \{fileID: ([0-9]+), guid: 32a564042a73e9c48bed4074dc75f29a, type: 3\}\s+propertyPath: (\S+)\s+value: ([^\r\n]*)"))
            {
                string id = item.Groups[1].Value;
                if (id != environmentId && id != fillId) continue;
                Apply(destination, item.Groups[2].Value, item.Groups[3].Value, id == fillId);
            }
        }

        public static string Baseline()
        {
            string path = Path.GetFullPath("../artifacts/light-profile-refactor-20261009/author-baseline/Game/Assets/DarkNights/Res/Shared/Lighting/LightEffect.prefab");
            if (!File.Exists(path)) throw new InvalidOperationException("旧作者值保全文件缺失，拒绝按新类默认值迁移：" + path);
            return File.ReadAllText(path);
        }

        private static string Document(string source, string name)
        {
            var documents = Regex.Split(source, @"(?m)(?=^--- !u!)");
            return documents.Single(value => value.Contains("::DarkNights.View.Lighting." + name));
        }

        private static float Number(string source, string field)
        {
            var match = Regex.Match(source, @"(?m)^  " + Regex.Escape(field) + @": ([^\r\n]+)");
            if (!match.Success) throw new InvalidOperationException("旧光效字段缺失：" + field);
            return Parse(match.Groups[1].Value);
        }

        private static Color ColorValue(string source)
        {
            var match = Regex.Match(source, @"color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}");
            if (!match.Success) throw new InvalidOperationException("旧光效颜色缺失。");
            return new Color(Parse(match.Groups[1].Value), Parse(match.Groups[2].Value), Parse(match.Groups[3].Value), Parse(match.Groups[4].Value));
        }

        private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);

        private static void Apply(LightProfile target, string name, string value, bool fill)
        {
            if (name == "m_Enabled") { if (fill) target.LocalFillEnabled = Parse(value) != 0; else target.EnvironmentEnabled = Parse(value) != 0; return; }
            if (name.StartsWith("color.", StringComparison.Ordinal))
            {
                Color color = fill ? target.FillColor : target.Color; float number = Parse(value);
                switch (name.Substring(6)) { case "r": color.r = number; break; case "g": color.g = number; break; case "b": color.b = number; break; case "a": color.a = number; break; default: throw new InvalidOperationException("未知颜色覆盖。"); }
                if (fill) target.FillColor = color; else target.Color = color; return;
            }
            string field;
            switch (name)
            {
                case "directional": target.Directional = Parse(value) != 0; return;
                case "range": field = "Range"; break; case "cone": field = "Cone"; break;
                case "apertureWidth": field = "ApertureWidth"; break; case "nearRange": field = "NearRange"; break;
                case "nearIntensity": field = "NearIntensity"; break; case "radius": field = "FillRadius"; break;
                case "intensity": field = fill ? "FillIntensity" : "Intensity"; break;
                default: return;
            }
            typeof(LightProfile).GetField(field).SetValue(target, Parse(value));
        }
    }
}
