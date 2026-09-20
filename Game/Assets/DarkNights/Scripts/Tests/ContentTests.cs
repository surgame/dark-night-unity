using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DarkNights.Core.Config;
using DarkNights.Editor;
using DarkNights.Runtime.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>
    /// 验证真实配置逐字段迁移、只读集合及错误输入边界。
    /// 直接读取仓库内冻结 JSON，不依赖 Godot 或参考目录，也不生成新的期望规则值。
    /// </summary>
    public sealed class ContentTests
    {
        private string balance;
        private string level;

        [SetUp]
        public void ReadContent()
        {
            balance = File.ReadAllText(GameContentSetup.ConfigRoot + "balance.json");
            level = File.ReadAllText(GameContentSetup.ConfigRoot + "pinewatch.json");
        }

        [Test]
        public void EverySourceRuleFieldSurvivesParsing()
        {
            GameCatalog catalog = GameCatalogJson.Parse(balance, level);
            var source = JObject.Parse(balance);
            var hero = (JObject)source["hero_control"];
            source.Remove("hero_control");
            int fields = Compare(source, catalog.Balance) + Compare(JObject.Parse(level), catalog.Level);
            int expected = source.Descendants().Concat(JObject.Parse(level).Descendants())
                .Count(t => t is JValue && !(t.Parent is JProperty property && property.Name == "sprite"));
            Assert.That(fields, Is.EqualTo(expected));
            Assert.That(Compare(hero, catalog.Balance.HeroControl), Is.EqualTo(8));
            Assert.That(catalog.Level.Seed, Is.EqualTo(90127UL));
            Assert.That(catalog.Level.Waves.Select(wave => wave.Enemies.Count), Is.EqualTo(new[] { 7, 11, 16 }));
            Assert.That(catalog.Balance.Worksites["food"].Amount, Is.EqualTo(-1));
            Assert.That(catalog.Balance.Units["worker"].Leash, Is.Zero);
            Assert.That(catalog.Balance.Buildings["house"].Damage.Count, Is.Zero);
        }

        [Test]
        public void SeedPreservesAllUnsignedBits()
        {
            JObject json = JObject.Parse(level);
            json["seed"] = ulong.MaxValue;
            Assert.That(GameCatalogJson.Parse(balance, json.ToString()).Level.Seed, Is.EqualTo(ulong.MaxValue));
        }

        [Test]
        public void CollectionsOwnTheirInputsAndCannotBeWritten()
        {
            GameCatalog catalog = GameCatalogJson.Parse(balance, level);
            var enemies = new List<string> { "zombie" };
            var wave = new WaveDefinition(90, 3.8, enemies, new ResourceAmounts());
            enemies[0] = "ghoul";
            Assert.That(wave.Enemies[0], Is.EqualTo("zombie"));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)wave.Enemies)[0] = "ghoul");
            var units = new Dictionary<string, UnitDefinition>(catalog.Balance.Units);
            var rules = new BalanceDefinition(1, catalog.Balance.Economy, units,
                catalog.Balance.Buildings, catalog.Balance.Worksites);
            units.Clear();
            Assert.That(rules.Units.Count, Is.EqualTo(catalog.Balance.Units.Count));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, UnitDefinition>)rules.Units).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<int>)rules.Units["worker"].Damage)[0] = 999);
        }

        [Test]
        public void ResourceArithmeticPreservesOriginalValueAndDoesNotClamp()
        {
            var original = new ResourceAmounts(60, 100, 80, 40, 0);
            var cost = new ResourceAmounts(12, 25);
            Assert.That(original.Subtract(cost).Food, Is.EqualTo(48));
            Assert.That(original.Subtract(cost).Add(cost).Wood, Is.EqualTo(original.Wood));
            Assert.That(original.With("gold", 7).Gold, Is.EqualTo(7));
            Assert.That(original.Gold, Is.Zero);
            Assert.That(new ResourceAmounts().Subtract(cost).Food, Is.EqualTo(-12));
            Assert.That(new ResourceAmounts(double.NaN).IsValid(), Is.False);
            Assert.That(new ResourceAmounts(double.PositiveInfinity).IsValid(), Is.False);
            Assert.Throws<ArgumentException>(() => original.Get("unknown"));
        }

        [TestCase("version")]
        [TestCase("missing")]
        [TestCase("string_number")]
        [TestCase("infinity")]
        [TestCase("negative")]
        [TestCase("fractional_integer")]
        [TestCase("damage_order")]
        [TestCase("damage_length")]
        [TestCase("null_cost")]
        [TestCase("unknown_enemy")]
        [TestCase("negative_seed")]
        [TestCase("zero_interval")]
        [TestCase("empty_waves")]
        [TestCase("duplicate")]
        [TestCase("trailing")]
        [TestCase("oversize")]
        public void RejectsInvalidConfiguration(string failure)
        {
            JObject rules = JObject.Parse(balance);
            JObject scenario = JObject.Parse(level);
            switch (failure)
            {
                case "version": rules["schema_version"] = 2; break;
                case "missing": ((JObject)rules["economy"]).Remove("upkeep_interval"); break;
                case "string_number": rules["units"]["worker"]["hp"] = "10"; break;
                case "infinity": rules["units"]["worker"]["hp"] = double.PositiveInfinity; break;
                case "negative": rules["units"]["worker"]["hp"] = -1; break;
                case "fractional_integer": rules["economy"]["training_queue_limit"] = 1.5; break;
                case "damage_order": rules["units"]["worker"]["damage"] = new JArray(3, 1); break;
                case "damage_length": rules["units"]["worker"]["damage"] = new JArray(1); break;
                case "null_cost": rules["units"]["worker"]["cost"] = null; break;
                case "unknown_enemy": scenario["waves"][0]["enemies"][0] = "unknown"; break;
                case "negative_seed": scenario["seed"] = -1; break;
                case "zero_interval": scenario["waves"][0]["spawn_interval"] = 0; break;
                case "empty_waves": scenario["waves"] = new JArray(); break;
            }
            string input = rules.ToString();
            if (failure == "duplicate") input = input.Replace("\"schema_version\": 1", "\"schema_version\": 1, \"schema_version\": 1");
            if (failure == "trailing") input += " {}";
            if (failure == "oversize") input = new string(' ', 1024 * 1024 + 1);
            Assert.Catch(() => GameCatalogJson.Parse(input, scenario.ToString()));
        }

        private static int Compare(JToken expected, object actual)
        {
            Assert.That(actual, Is.Not.Null, expected.Path);
            if (expected is JObject fields)
            {
                int count = 0;
                foreach (JProperty field in fields.Properties())
                {
                    // Godot 的 sprite 字段未进入原规则定义；原始字节仍保留，外观映射在后续对象批次处理。
                    if (field.Name == "sprite") continue;
                    object value;
                    if (actual is IDictionary dictionary) value = dictionary[field.Name];
                    else
                    {
                        var property = actual.GetType().GetProperties().SingleOrDefault(info =>
                            info.Name == field.Name || Regex.Replace(info.Name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant() == field.Name);
                        Assert.That(property, Is.Not.Null, field.Path);
                        value = property.GetValue(actual);
                    }
                    count += Compare(field.Value, value);
                }
                return count;
            }
            if (expected is JArray items)
            {
                object[] values = ((IEnumerable)actual).Cast<object>().ToArray();
                Assert.That(values.Length, Is.EqualTo(items.Count), expected.Path);
                return items.Select((item, index) => Compare(item, values[index])).Sum();
            }
            if (expected.Type == JTokenType.Integer || expected.Type == JTokenType.Float)
                Assert.That(Convert.ToDouble(actual), Is.EqualTo((double)expected), expected.Path);
            else Assert.That(actual.ToString(), Is.EqualTo((string)expected), expected.Path);
            return 1;
        }
    }
}
