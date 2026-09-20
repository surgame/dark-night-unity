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
using static DarkNights.Tests.SessionScenario;

namespace DarkNights.Tests
{
    /// <summary>
    /// v3 格式、内容身份和完整恢复回归，使用真实 YYGC 对象及二十秒继续模拟。
    /// 所有破坏性输入只作用于测试持有的 JSON；不更改冻结夹具、正式布局或活动世界。
    /// </summary>
    public static class GameSaveScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            var codec = Codec(catalog, layout);
            using var active = World(catalog, layout);
            active.IssueOrders(new[] { 11 }, 6, 402);
            active.PlaceBuilding("house", 184, new[] { 12 });
            active.TrainActors("archer", new[] { 13 });
            for (int i = 0; i < 360; i++) active.Advance(1.0 / 60);
            SessionSnapshot snapshot = active.CaptureWorld();
            string before = codec.Serialize(snapshot);
            var root = JObject.Parse(before);
            check((string)root["format"] == ObjectWorldSaveJson.Format && (int)root["format_version"] == ObjectWorldSaveJson.FormatVersion &&
                (string)root["random_algorithm"] == SimulationRandom.Algorithm, "Save has explicit v3 format and RNG identity");
            check(root["world"]["camera_x"] == null && root["world"]["selected_ids"] == null &&
                root["world"]["epoch"] == null && root["world"]["control_mode"] == null,
                "Save excludes local display and room state");
            using var restored = World(catalog, layout);
            restored.Restore(before);
            check(codec.Serialize(restored.CaptureWorld()) == before, "V2 restores every persisted field");
            using var continued = World(catalog, layout);
            continued.Restore(before);
            for (int i = 0; i < 1200; i++) { restored.Advance(1.0 / 60); continued.Advance(1.0 / 60); }
            check(codec.Serialize(restored.CaptureWorld()) == codec.Serialize(continued.CaptureWorld()),
                "Independent restored worlds continue twenty seconds deterministically");
            check(codec.Serialize(active.CaptureWorld()) == before && codec.Serialize(snapshot) == before,
                "Restored state and background capture never alias the original");
            CheckTime(check, codec, catalog, layout);
            var changes = new Dictionary<string, Action<JObject>>
            {
                ["format"] = j => j["format"] = "other-game",
                ["old version"] = j => j["format_version"] = 1,
                ["future version"] = j => j["format_version"] = ObjectWorldSaveJson.FormatVersion + 1,
                ["version type"] = j => j["format_version"] = "1",
                ["definition digest"] = j => j["identity_sha256"] = new string('0', 64),
                ["missing identities"] = j => ((JObject)j["world"]).Remove("identities"),
                ["unknown definition"] = j => j["world"]["identities"][0]["definition_guid"] = new string('0', 32),
                ["duplicate placement"] = j => j["world"]["identities"][1]["placement_key"] = j["world"]["identities"][0]["placement_key"].DeepClone(),
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
                check(Rejected(() => active.Restore(invalid.ToString())) && codec.Serialize(active.CaptureWorld()) == before,
                    "Invalid new save preserves active world: " + change.Key);
            }
            foreach (string text in new[]
            {
                RuleScenario.Fixture("legacy-v1.json").ToString(), before + "{}", before.Replace("\"world\":", "\"format\":\"duplicate\",\"world\":"),
                new string(' ', ObjectWorldSaveJson.MaximumBytes + 1), "{\"padding\":\"" + new string('谷', 1400000) + "\"}",
                new string('[', 40) + new string(']', 40)
            })
                check(Rejected(() => active.Restore(text)), "New format rejects legacy, trailing, duplicate, oversized or deep input");
            CheckFingerprint(check, catalog, layout, before);
        }

        private static void CheckTime(Action<bool, string> check, ObjectWorldSaveJson codec, GameCatalog catalog, LevelLayout layout)
        {
            using var game = World(catalog, layout);
            game.SetTime(true, 2);
            using var restored = World(catalog, layout);
            restored.Restore(codec.Serialize(game.CaptureWorld()));
            for (int i = 0; i < 60; i++) restored.Advance(1.0 / 60);
            check(restored.Paused && restored.Speed == 2 && restored.Elapsed == 0, "New save retains pause and speed");
            restored.SetTime(false, 2);
            for (int i = 0; i < 60; i++) restored.Advance(1.0 / 60);
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
                balance.Worksites.Reverse().ToDictionary(p => p.Key, p => p.Value), balance.HeroControl, balance.Expedition), catalog.Level);
            check(new SaveContentFingerprint(reordered, layout).RulesSha256 == baseline.RulesSha256,
                "Dictionary insertion order does not change rules digest");
            var cameraOnly = CopyLayout(layout, layout.Actors, layout.CameraX + 1);
            check(new SaveContentFingerprint(catalog, cameraOnly).LayoutSha256 == baseline.LayoutSha256,
                "Local camera default does not change world compatibility");
            var changedOrder = CopyLayout(layout, layout.Actors.Reverse().ToArray(), layout.CameraX);
            check(Rejected(() => Codec(catalog, changedOrder).Parse(saved)), "Changed spawn order rejects old save");
            var moved = layout.Actors.Select((p, i) => i == 0 ? new PlacementDefinition(p.Kind, p.X + 1, p.Variant, p.Name) : p).ToArray();
            check(Rejected(() => Codec(catalog, CopyLayout(layout, moved, layout.CameraX)).Parse(saved)),
                "Changed scene layout rejects old save");
            string configRoot = Path.Combine(RuleScenario.RepositoryRoot, "Game/Assets/DarkNights/Res/Config");
            JObject rules = JObject.Parse(File.ReadAllText(Path.Combine(configRoot, "balance.json")));
            string level = File.ReadAllText(Path.Combine(configRoot, "pinewatch.json"));
            rules["units"]["worker"]["hp"] = (double)rules["units"]["worker"]["hp"] + 1;
            check(Rejected(() => Codec(GameCatalogJson.Parse(rules.ToString(), level), layout).Parse(saved)),
                "Changed actual rule values reject old save");
            var seedChanged = new GameCatalog(balance, new LevelDefinition(catalog.Level.Id, catalog.Level.Name,
                catalog.Level.Seed + 1, catalog.Level.Waves));
            check(Rejected(() => Codec(seedChanged, layout).Parse(saved)), "Changed 64-bit level seed rejects old save");
        }

        private static LevelLayout CopyLayout(LevelLayout layout, IReadOnlyList<PlacementDefinition> actors, float cameraX) =>
            new LevelLayout(layout.WorldWidth, layout.GroundY, layout.BuildMinX, layout.BuildMaxX,
                layout.SpawnX, cameraX, layout.Buildings, layout.Worksites, actors, layout.Platforms);

        public static bool Rejected(Action action)
        {
            try { action(); return false; }
            catch (Exception error) when (error is ArgumentException || error is FormatException || error is JsonException)
            { return true; }
        }
    }
}
