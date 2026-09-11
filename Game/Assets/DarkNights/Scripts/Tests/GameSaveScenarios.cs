using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic;
using DarkNights.Core.Save;
using DarkNights.Runtime.Config;
using DarkNights.Runtime.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkNights.Tests
{
    /// <summary>
    /// 新档格式、内容兼容和完整恢复的共同回归，用冻结旧档及其二十秒结果验证跨格式迁移。
    /// 所有破坏性输入只作用于测试持有的 JSON；不更改冻结夹具、正式布局或活动世界。
    /// </summary>
    public static class GameSaveScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            var codec = new GameSaveJson(catalog, layout);
            JObject legacy = RuleScenario.Fixture("legacy-v1.json");
            GameSession active = codec.ImportLegacy(legacy.ToString());
            string before = codec.Serialize(SnapshotMapper.Capture(active));
            var root = JObject.Parse(before);
            check((string)root["format"] == GameSaveJson.Format && (int)root["format_version"] == 1 &&
                (string)root["random_algorithm"] == GameSaveJson.RandomAlgorithm, "New save has explicit format and RNG identity");
            var display = new LegacyDisplayState(200, 2, new[] { active.World.Actors[0].Id });
            check(codec.Serialize(SnapshotMapper.Capture(active, display)) == before &&
                root["world"]["camera_x"] == null && root["world"]["selected_ids"] == null &&
                root["world"]["epoch"] == null && root["world"]["control_mode"] == null,
                "New save excludes local display and room state");
            var restored = codec.Restore(before);
            check(!ReferenceEquals(active, restored) && codec.Serialize(SnapshotMapper.Capture(restored)) == before,
                "New save restores a separate complete world without changing its fields");
            RuleScenario.Step(restored, 20);
            JObject continued = JObject.Parse(LegacySnapshotJson.Serialize(SnapshotMapper.Capture(restored)));
            JObject expected = RuleScenario.Fixture("legacy-v1-after-20s.json");
            foreach (string field in new[] { "camera_x", "camera_zoom", "selected_ids" })
            {
                continued.Remove(field);
                expected.Remove(field);
            }
            string difference = RuleScenario.Difference(continued, expected);
            check(difference.Length == 0, "New save continuation matches frozen 20s result: " + difference);
            check(codec.Serialize(SnapshotMapper.Capture(active)) == before, "Restored world does not alias original world");
            CheckTime(check, codec, catalog, layout);
            var changes = new Dictionary<string, Action<JObject>>
            {
                ["format"] = j => j["format"] = "other-game",
                ["version"] = j => j["format_version"] = 2,
                ["version type"] = j => j["format_version"] = "1",
                ["RNG algorithm"] = j => j["random_algorithm"] = "system-random",
                ["rules digest"] = j => j["rules_sha256"] = new string('0', 64),
                ["layout digest"] = j => j["layout_sha256"] = new string('0', 64),
                ["missing digest"] = j => j.Remove("rules_sha256"),
                ["unknown envelope field"] = j => j["extra"] = 1,
                ["null world"] = j => j["world"] = null,
                ["missing world field"] = j => ((JObject)j["world"]).Remove("elapsed"),
                ["local display"] = j => j["world"]["camera_x"] = 100,
                ["room policy"] = j => j["world"]["control_mode"] = "SharedCamp",
                ["duplicate entity"] = j => j["world"]["actors"][0]["id"] = j["world"]["buildings"][0]["id"].DeepClone(),
                ["invalid target"] = j => j["world"]["actors"][0]["target_id"] = 999,
                ["nonfinite time"] = j => j["world"]["elapsed"] = double.NaN,
                ["numeric RNG"] = j => j["world"]["rng_seed"] = 123,
                ["overflow RNG"] = j => j["world"]["rng_state"] = "18446744073709551616",
                ["negative inventory"] = j => j["world"]["economy"]["resources"]["food"] = -1,
                ["oversized entities"] = j => j["world"]["actors"] = new JArray(
                    Enumerable.Range(0, 257).Select(i => j["world"]["actors"][0].DeepClone()))
            };
            foreach (var change in changes)
            {
                var invalid = (JObject)root.DeepClone();
                change.Value(invalid);
                check(Rejected(() => codec.Restore(invalid.ToString())) && codec.Serialize(SnapshotMapper.Capture(active)) == before,
                    "Invalid new save preserves active world: " + change.Key);
            }
            foreach (string text in new[]
            {
                legacy.ToString(), before + "{}", before.Replace("\"world\":", "\"format\":\"duplicate\",\"world\":"),
                new string(' ', GameSaveJson.MaximumBytes + 1), "{\"padding\":\"" + new string('谷', 1400000) + "\"}",
                new string('[', 40) + new string(']', 40)
            })
                check(Rejected(() => codec.Restore(text)), "New format rejects legacy, trailing, duplicate, oversized or deep input");
            check(Rejected(() => codec.ImportLegacy(before)), "Legacy import does not guess the new format");
            CheckFingerprint(check, catalog, layout, before);
        }

        private static void CheckTime(Action<bool, string> check, GameSaveJson codec, GameCatalog catalog, LevelLayout layout)
        {
            var game = new GameSession(catalog, layout) { Speed = 2, Paused = true };
            var restored = codec.Restore(codec.Serialize(SnapshotMapper.Capture(game)));
            RuleScenario.Step(restored, 1);
            check(restored.Paused && restored.Speed == 2 && restored.Elapsed == 0, "New save retains pause and speed");
            restored.Paused = false;
            RuleScenario.Step(restored, 1);
            check(RuleScenario.Approx(restored.Elapsed, 2), "Restored speed applies once when simulation resumes");
        }

        private static void CheckFingerprint(Action<bool, string> check, GameCatalog catalog, LevelLayout layout, string saved)
        {
            var baseline = new SaveContentFingerprint(catalog, layout);
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var other = new SaveContentFingerprint(catalog, layout);
                check(baseline.RulesSha256 == other.RulesSha256 && baseline.LayoutSha256 == other.LayoutSha256,
                    "Content digests are independent of current culture");
            }
            finally { CultureInfo.CurrentCulture = previous; }
            BalanceDefinition balance = catalog.Balance;
            var reordered = new GameCatalog(new BalanceDefinition(balance.SchemaVersion, balance.Economy,
                balance.Units.Reverse().ToDictionary(p => p.Key, p => p.Value),
                balance.Buildings.Reverse().ToDictionary(p => p.Key, p => p.Value),
                balance.Worksites.Reverse().ToDictionary(p => p.Key, p => p.Value)), catalog.Level);
            check(new SaveContentFingerprint(reordered, layout).RulesSha256 == baseline.RulesSha256,
                "Dictionary insertion order does not change rules digest");
            var cameraOnly = CopyLayout(layout, layout.Actors, layout.CameraX + 1);
            check(new SaveContentFingerprint(catalog, cameraOnly).LayoutSha256 == baseline.LayoutSha256,
                "Local camera default does not change world compatibility");
            var changedOrder = CopyLayout(layout, layout.Actors.Reverse().ToArray(), layout.CameraX);
            check(Rejected(() => new GameSaveJson(catalog, changedOrder).Restore(saved)), "Changed spawn order rejects old save");
            var moved = layout.Actors.Select((p, i) => i == 0 ? new PlacementDefinition(p.Kind, p.X + 1, p.Variant, p.Name) : p).ToArray();
            check(Rejected(() => new GameSaveJson(catalog, CopyLayout(layout, moved, layout.CameraX)).Restore(saved)),
                "Changed scene layout rejects old save");
            string configRoot = Path.Combine(RuleScenario.RepositoryRoot, "Game/Assets/DarkNights/Res/Config");
            JObject rules = JObject.Parse(File.ReadAllText(Path.Combine(configRoot, "balance.json")));
            string level = File.ReadAllText(Path.Combine(configRoot, "pinewatch.json"));
            rules["units"]["worker"]["hp"] = (double)rules["units"]["worker"]["hp"] + 1;
            check(Rejected(() => new GameSaveJson(GameCatalogJson.Parse(rules.ToString(), level), layout).Restore(saved)),
                "Changed actual rule values reject old save");
            var seedChanged = new GameCatalog(balance, new LevelDefinition(catalog.Level.Id, catalog.Level.Name,
                catalog.Level.Seed + 1, catalog.Level.Waves));
            check(Rejected(() => new GameSaveJson(seedChanged, layout).Restore(saved)), "Changed 64-bit level seed rejects old save");
        }

        private static LevelLayout CopyLayout(LevelLayout layout, IReadOnlyList<PlacementDefinition> actors, float cameraX) =>
            new LevelLayout(layout.WorldWidth, layout.GroundY, layout.BuildMinX, layout.BuildMaxX,
                layout.SpawnX, cameraX, layout.Buildings, layout.Worksites, actors);

        public static bool Rejected(Action action)
        {
            try { action(); return false; }
            catch (Exception error) when (error is ArgumentException || error is FormatException || error is JsonException)
            { return true; }
        }
    }
}
