using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic;
using DarkNights.Core.Save;
using DarkNights.Runtime.Save;
using Newtonsoft.Json.Linq;

namespace DarkNights.Tests
{
    /// <summary>
    /// 使用冻结旧档验证完整恢复及继续二十秒，并攻击字段、关系、输入和快照所有权边界。
    /// 不重生成期望状态；同一检查同时在独立进程及 Unity Editor 运行。
    /// </summary>
    public static class SaveMigrationScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            JObject original = RuleScenario.Fixture("legacy-v1.json");
            var saved = LegacySnapshotJson.Deserialize(original.ToString());
            check(SnapshotValidator.Validate(saved, catalog, layout).Length == 0, "Frozen v1 save passes field and relationship validation");
            var game = SnapshotMapper.Restore(saved, catalog, layout);
            var display = new LegacyDisplayState((float)saved.CameraX, (float)saved.CameraZoom, saved.SelectedIds);
            string difference = RuleScenario.Difference(JObject.Parse(LegacySnapshotJson.Serialize(SnapshotMapper.Capture(game, display))), original);
            check(difference.Length == 0, "Legacy restore retains every field: " + difference);
            var heldSnapshot = SnapshotMapper.Capture(game, display);
            string frozen = LegacySnapshotJson.Serialize(heldSnapshot);
            RuleScenario.Step(game, 20);
            difference = RuleScenario.Difference(JObject.Parse(LegacySnapshotJson.Serialize(SnapshotMapper.Capture(game, display))),
                RuleScenario.Fixture("legacy-v1-after-20s.json"));
            check(difference.Length == 0, "Legacy continuation after 20s matches frozen result: " + difference);
            check(LegacySnapshotJson.Serialize(heldSnapshot) == frozen, "Captured save collections and nested records remain frozen after simulation");
            var captured = SnapshotMapper.Capture(game);
            bool immutable = false;
            try { ((IList<ActorSnapshot>)captured.Actors).Clear(); }
            catch (NotSupportedException) { immutable = true; }
            check(immutable, "Save actor collection cannot be mutated through IList");
            string current = LegacySnapshotJson.Serialize(captured);
            var changes = new Dictionary<string, Action<JObject>>
            {
                ["missing elapsed"] = j => j.Remove("elapsed"),
                ["null actor"] = j => j["actors"][0] = null,
                ["unknown actor kind"] = j => j["actors"][0]["kind"] = "missing",
                ["negative resource"] = j => j["economy"]["resources"]["food"] = -1,
                ["missing resource"] = j => ((JObject)j["economy"]["resources"]).Remove("wood"),
                ["invalid enum"] = j => j["actors"][0]["state"] = "unknown",
                ["numeric enum"] = j => j["actors"][0]["state"] = 0,
                ["numeric string"] = j => j["elapsed"] = "1",
                ["nonfinite time"] = j => j["elapsed"] = double.NaN,
                ["duplicate identity"] = j => j["actors"][0]["id"] = j["buildings"][0]["id"].DeepClone(),
                ["broken work relation"] = j => j["worksites"][0]["worker_id"] = 999,
                ["broken target"] = j => j["actors"][0]["target_id"] = 999,
                ["invalid random state"] = j => j["rng_state"] = "18446744073709551616",
                ["noncanonical random state"] = j => j["rng_state"] = "+123",
                ["unsupported speed"] = j => j["speed"] = 3,
                ["oversized collection"] = j => j["actors"] = new JArray(Enumerable.Range(0, 257).Select(i => j["actors"][0].DeepClone())),
                ["projectile shape"] = j => j["projectiles"][0]["from"] = new JArray(1),
                ["projectile damage"] = j => j["projectiles"][0]["damage"] = -1
            };
            foreach (var change in changes)
            {
                var bad = (JObject)original.DeepClone();
                change.Value(bad);
                bool rejected = false;
                try { SnapshotMapper.Restore(LegacySnapshotJson.Deserialize(bad.ToString()), catalog, layout); }
                catch (Exception error) when (error is ArgumentException || error is FormatException) { rejected = true; }
                check(rejected && LegacySnapshotJson.Serialize(SnapshotMapper.Capture(game)) == current,
                    "Invalid save preserves active world: " + change.Key);
            }
            foreach (string invalid in new[]
            {
                "{\"schema_version\":1,\"schema_version\":1}",
                original.ToString() + "{}",
                new string(' ', LegacySnapshotJson.MaximumBytes + 1),
                "{\"padding\":\"" + new string('谷', 1400000) + "\"}"
            })
            {
                bool rejected = false;
                try { LegacySnapshotJson.Deserialize(invalid); }
                catch (Exception error) when (error is FormatException || error is Newtonsoft.Json.JsonException) { rejected = true; }
                check(rejected, "JSON boundary rejects duplicate keys, trailing data or oversized input");
            }
            CheckCommands(check, catalog, layout);
        }

        private static void CheckCommands(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            var game = new GameSession(catalog, layout);
            int worker = RuleScenario.Workers(game)[0].Id;
            string before = LegacySnapshotJson.Serialize(SnapshotMapper.Capture(game));
            check(!game.Construction.Place("house", float.NaN, new[] { worker }) &&
                !game.Construction.Place("house", 184, new[] { 999 }) &&
                game.Training.Start("archer", new[] { worker, worker }) == 0 &&
                game.Orders.Issue(new[] { worker }, 999, 300) == 0 &&
                game.Orders.Issue(new[] { worker }, 0, float.PositiveInfinity) == 0 &&
                LegacySnapshotJson.Serialize(SnapshotMapper.Capture(game)) == before,
                "Malformed explicit commands neither pay nor modify world");
            int second = RuleScenario.Workers(game)[1].Id;
            game.Orders.Issue(new[] { worker }, 0, 200);
            game.Orders.Issue(new[] { second }, 0, 400);
            check(game.World.Find<DarkNights.Core.Logic.Entities.Actor>(worker).MoveX == 200 &&
                game.World.Find<DarkNights.Core.Logic.Entities.Actor>(second).MoveX == 400,
                "Two explicit selections retain independent targets");
            var other = new GameSession(catalog, layout);
            check(!game.Work.Assign(RuleScenario.Workers(other)[0], RuleScenario.Site(game, "wood")) &&
                !game.Work.Assign(RuleScenario.Workers(game)[0], RuleScenario.Site(other, "wood")),
                "A foreign world entity cannot enter local work relationships");
        }
    }
}
