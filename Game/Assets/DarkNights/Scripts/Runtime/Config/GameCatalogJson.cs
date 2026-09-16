using System;
using System.Collections.Generic;
using DarkNights.Core.Config;
using Newtonsoft.Json.Linq;

namespace DarkNights.Runtime.Config
{
    /// <summary>
    /// 将原始 balance 与关卡 JSON 显式转换成 Core 的不可变配置。
    /// 字段映射与默认值沿用 Godot 基线，无反射构造、运行时类型名或磁盘路径依赖，适用于 IL2CPP 裁剪。
    /// </summary>
    public static class GameCatalogJson
    {
        public static GameCatalog Parse(string balanceJson, string levelJson)
        {
            JObject balance = ConfigJson.Parse(balanceJson);
            JObject level = ConfigJson.Parse(levelJson);
            var rules = new BalanceDefinition(ConfigJson.Integer(balance, "schema_version"),
                Economy(ConfigJson.Object(balance, "economy")),
                Dictionary(balance, "units", Unit), Dictionary(balance, "buildings", Building),
                Dictionary(balance, "worksites", Worksite), HeroControl(ConfigJson.Object(balance, "hero_control")));
            var waves = new List<WaveDefinition>();
            foreach (JToken wave in ConfigJson.Array(level, "waves"))
            {
                if (!(wave is JObject)) throw new FormatException("Wave must be an object.");
                var enemies = new List<string>();
                foreach (JToken enemy in ConfigJson.Array(wave, "enemies"))
                {
                    if (enemy.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)enemy))
                        throw new FormatException("Enemy must be a content ID.");
                    enemies.Add((string)enemy);
                }
                waves.Add(new WaveDefinition(ConfigJson.Number(wave, "day_seconds"),
                    ConfigJson.Positive(wave, "spawn_interval"), enemies, Resources(wave, "reward")));
            }
            return new GameCatalog(rules, new LevelDefinition(ConfigJson.Text(level, "id"),
                ConfigJson.Text(level, "name"), ConfigJson.Seed(level), waves));
        }

        private static Dictionary<string, T> Dictionary<T>(JToken parent, string key, Func<JToken, T> convert)
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (JProperty entry in ConfigJson.Object(parent, key).Properties())
            {
                if (string.IsNullOrWhiteSpace(entry.Name) || !(entry.Value is JObject))
                    throw new FormatException("Invalid content entry: " + entry.Path);
                result.Add(entry.Name, convert(entry.Value));
            }
            return result;
        }

        private static HeroControlDefinition HeroControl(JToken value) => new HeroControlDefinition(
            ConfigJson.Positive(value, "jump_speed"), ConfigJson.Positive(value, "gravity"),
            ConfigJson.Positive(value, "maximum_height"), ConfigJson.Positive(value, "jetpack_speed"),
            ConfigJson.Positive(value, "fuel_seconds"), ConfigJson.Positive(value, "fuel_recovery"),
            ConfigJson.Positive(value, "drop_seconds"), ConfigJson.Positive(value, "work_reach"));

        private static EconomyDefinition Economy(JToken value) => new EconomyDefinition(
            Resources(value, "starting_resources"), ConfigJson.Positive(value, "upkeep_interval"),
            ConfigJson.Number(value, "food_per_person"), ConfigJson.Positive(value, "starvation_interval"),
            Resources(value, "recruit_cost"), ConfigJson.Positive(value, "recruit_seconds"),
            Resources(value, "repair_cost"), ConfigJson.Positive(value, "repair_hp"),
            ConfigJson.Positive(value, "training_seconds"), ConfigJson.Integer(value, "training_queue_limit", minimum: 1));

        private static UnitDefinition Unit(JToken value)
        {
            double attack = ConfigJson.Positive(value, "attack_seconds");
            double windup = ConfigJson.Number(value, "windup");
            if (windup > attack) throw ConfigJson.Error(value, "windup", "must not exceed attack_seconds");
            return new UnitDefinition(ConfigJson.Text(value, "name"), ConfigJson.Positive(value, "hp"),
                ConfigJson.Number(value, "armor"), Damage(value, false), ConfigJson.Number(value, "speed"),
                ConfigJson.Number(value, "range"), ConfigJson.Number(value, "aggro"), ConfigJson.Number(value, "leash", true),
                attack, windup, ConfigJson.Integer(value, "gold", true), Resources(value, "cost", true));
        }

        private static BuildingDefinition Building(JToken value) => new BuildingDefinition(
            ConfigJson.Text(value, "name"), ConfigJson.Text(value, "description"), ConfigJson.Positive(value, "hp"),
            ConfigJson.Width(value), ConfigJson.Positive(value, "build_seconds"), ConfigJson.Integer(value, "capacity", true),
            Resources(value, "cost", true), ConfigJson.Number(value, "range", true), Damage(value, true),
            ConfigJson.Number(value, "attack_seconds", true));

        private static WorksiteDefinition Worksite(JToken value) => new WorksiteDefinition(
            ConfigJson.Text(value, "name"), ConfigJson.Integer(value, "amount", minimum: -1),
            ConfigJson.Integer(value, "yield", minimum: 1), ConfigJson.Positive(value, "interval"), ConfigJson.Width(value));

        private static IReadOnlyList<int> Damage(JToken value, bool optional)
        {
            JArray damage = ConfigJson.Array(value, "damage", optional);
            if (optional && value["damage"] == null) return System.Array.Empty<int>();
            if (damage.Count != 2) throw ConfigJson.Error(value, "damage", "two integers required");
            var result = new List<int>();
            foreach (JToken number in damage)
            {
                if (number.Type != JTokenType.Integer || (double)number < 0 || (double)number > int.MaxValue)
                    throw ConfigJson.Error(value, "damage", "nonnegative integer required");
                result.Add((int)number);
            }
            if (result[0] > result[1]) throw ConfigJson.Error(value, "damage", "minimum exceeds maximum");
            return result;
        }

        private static ResourceAmounts Resources(JToken parent, string key, bool optional = false)
        {
            if (parent[key] == null && optional) return new ResourceAmounts();
            JObject value = ConfigJson.Object(parent, key);
            var result = new ResourceAmounts(ConfigJson.Number(value, "food", true), ConfigJson.Number(value, "wood", true),
                ConfigJson.Number(value, "stone", true), ConfigJson.Number(value, "iron", true), ConfigJson.Number(value, "gold", true));
            if (!result.IsValid()) throw ConfigJson.Error(parent, key, "resource amount exceeds allowed range");
            return result;
        }
    }
}
