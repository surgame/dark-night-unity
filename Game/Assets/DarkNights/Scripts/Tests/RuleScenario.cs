using System;
using System.IO;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Runtime.Config;
using Newtonsoft.Json.Linq;

namespace DarkNights.Tests
{
    /// <summary>
    /// 规则回归的固定 60 Hz 驱动及冻结夹具读取器，仅在测试程序集使用。
    /// 布局来自历史证据供跨引擎对照；正式启动不能引用此夹具或用它替代场景标记。
    /// </summary>
    public static class RuleScenario
    {
        public static string RepositoryRoot { get; set; }

        public static JObject Fixture(string name) => JObject.Parse(File.ReadAllText(
            Path.Combine(RepositoryRoot, "tools", "CoreRegression", "Fixtures", name)));

        public static GameCatalog Catalog() => GameCatalogJson.Parse(
            File.ReadAllText(Path.Combine(RepositoryRoot, "Game/Assets/DarkNights/Res/Config/balance.json")),
            File.ReadAllText(Path.Combine(RepositoryRoot, "Game/Assets/DarkNights/Res/Config/pinewatch.json")));

        public static LevelLayout Layout()
        {
            JObject f = Fixture("pinewatch-layout-v1.json");
            Func<string, PlacementDefinition[]> placements = key => ((JArray)f[key]).Select(p =>
                new PlacementDefinition((string)p["kind"], (float)p["x"], (int?)p["variant"] ?? 0, (string)p["name"] ?? "")).ToArray();
            return new LevelLayout((float)f["world_width"], (float)f["ground_y"], (float)f["build_min_x"],
                (float)f["build_max_x"], (float)f["spawn_x"], (float)f["camera_x"],
                placements("buildings"), placements("worksites"), placements("actors"));
        }

        public static bool Approx(double a, double b) => Math.Abs(a - b) <= 0.0001;

        public static string Difference(JToken actual, JToken expected, string path = "$")
        {
            if (actual == null || expected == null) return actual == expected ? "" : path + ": missing";
            bool aNumber = actual.Type == JTokenType.Integer || actual.Type == JTokenType.Float;
            bool bNumber = expected.Type == JTokenType.Integer || expected.Type == JTokenType.Float;
            if (aNumber && bNumber) return Approx((double)actual, (double)expected) ? "" : path + ": " + actual + " != " + expected;
            if (actual.Type != expected.Type) return path + ": type mismatch";
            if (actual is JObject ao && expected is JObject eo)
            {
                if (ao.Count != eo.Count) return path + ": field count";
                foreach (var p in eo.Properties())
                {
                    string diff = Difference(ao[p.Name], p.Value, path + "." + p.Name);
                    if (diff.Length != 0) return diff;
                }
                return "";
            }
            if (actual is JArray aa && expected is JArray ea)
            {
                if (aa.Count != ea.Count) return path + ": array length";
                for (int i = 0; i < aa.Count; i++)
                {
                    string diff = Difference(aa[i], ea[i], path + "[" + i + "]");
                    if (diff.Length != 0) return diff;
                }
                return "";
            }
            return JToken.DeepEquals(actual, expected) ? "" : path + ": " + actual + " != " + expected;
        }
    }
}
