using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DarkNights.Core.Config;

namespace DarkNights.Runtime.Save
{
    /// <summary>
    /// 从实际使用的不可变目录与场景导出布局计算存档兼容摘要，不读取源文件或表现资产。
    /// 固定字段顺序、字典序和二进制数值编码；布局保留出生顺序，算法变更必须升级新档格式。
    /// </summary>
    public sealed class SaveContentFingerprint
    {
        public string RulesSha256 { get; }
        public string LayoutSha256 { get; }

        public SaveContentFingerprint(GameCatalog catalog, LevelLayout layout)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            layout.Validate(catalog);
            RulesSha256 = Hash(writer => WriteRules(writer, catalog));
            LayoutSha256 = Hash(writer => WriteLayout(writer, layout));
        }

        private static string Hash(Action<BinaryWriter> write)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, new UTF8Encoding(false, true), true))
                    write(writer);
                using (var hash = SHA256.Create())
                    return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void WriteRules(BinaryWriter writer, GameCatalog catalog)
        {
            writer.Write("dark-nights.rules.v2");
            BalanceDefinition balance = catalog.Balance;
            writer.Write(balance.SchemaVersion);
            HeroControlDefinition hero = balance.HeroControl;
            writer.Write(hero != null);
            if (hero != null)
            {
                writer.Write(hero.JumpSpeed); writer.Write(hero.Gravity); writer.Write(hero.MaximumHeight);
                writer.Write(hero.JetpackSpeed); writer.Write(hero.FuelSeconds); writer.Write(hero.FuelRecovery);
                writer.Write(hero.DropSeconds); writer.Write(hero.WorkReach);
            }
            writer.Write(balance.Expedition.OxygenSeconds);
            writer.Write(balance.Expedition.BagCapacity);
            writer.Write(balance.Expedition.ShipCapacity);
            writer.Write(balance.Expedition.StorageCapacity);
            writer.Write(balance.Expedition.OxygenRadius);
            writer.Write(balance.Expedition.RelayRange);
            writer.Write(balance.Expedition.PowerSupply);
            writer.Write(balance.Expedition.DeploySeconds);
            writer.Write(balance.Expedition.ExtractSeconds);
            writer.Write(balance.Expedition.RecallSeconds);
            writer.Write(balance.Expedition.ThreatSeconds);
            writer.Write(balance.Expedition.ModulePrice);
            EconomyDefinition economy = balance.Economy;
            WriteResources(writer, economy.StartingResources);
            writer.Write(economy.UpkeepInterval);
            writer.Write(economy.FoodPerPerson);
            writer.Write(economy.StarvationInterval);
            WriteResources(writer, economy.RecruitCost);
            writer.Write(economy.RecruitSeconds);
            WriteResources(writer, economy.RepairCost);
            writer.Write(economy.RepairHp);
            writer.Write(economy.TrainingSeconds);
            writer.Write(economy.TrainingQueueLimit);
            WriteDictionary(writer, balance.Units, unit =>
            {
                writer.Write(unit.Name);
                writer.Write(unit.Hp);
                writer.Write(unit.Armor);
                writer.Write(unit.Damage.Count);
                foreach (int value in unit.Damage) writer.Write(value);
                writer.Write(unit.Speed);
                writer.Write(unit.Range);
                writer.Write(unit.Aggro);
                writer.Write(unit.Leash);
                writer.Write(unit.AttackSeconds);
                writer.Write(unit.Windup);
                writer.Write(unit.Gold);
                WriteResources(writer, unit.Cost);
            });
            WriteDictionary(writer, balance.Buildings, building =>
            {
                writer.Write(building.Name);
                writer.Write(building.Description);
                writer.Write(building.Hp);
                writer.Write(building.Width);
                writer.Write(building.BuildSeconds);
                writer.Write(building.Capacity);
                WriteResources(writer, building.Cost);
                writer.Write(building.Range);
                writer.Write(building.Damage.Count);
                foreach (int value in building.Damage) writer.Write(value);
                writer.Write(building.AttackSeconds);
            });
            WriteDictionary(writer, balance.Worksites, site =>
            {
                writer.Write(site.Name);
                writer.Write(site.Amount);
                writer.Write(site.Yield);
                writer.Write(site.Interval);
                writer.Write(site.Width);
            });
            LevelDefinition level = catalog.Level;
            writer.Write(level.Id);
            writer.Write(level.Name);
            writer.Write(level.Seed);
            writer.Write(level.Waves.Count);
            foreach (WaveDefinition wave in level.Waves)
            {
                writer.Write(wave.DaySeconds);
                writer.Write(wave.SpawnInterval);
                writer.Write(wave.Enemies.Count);
                foreach (string enemy in wave.Enemies) writer.Write(enemy);
                WriteResources(writer, wave.Reward);
            }
        }

        private static void WriteDictionary<T>(BinaryWriter writer, IReadOnlyDictionary<string, T> entries, Action<T> write)
        {
            writer.Write(entries.Count);
            foreach (var entry in entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.Write(entry.Key);
                write(entry.Value);
            }
        }

        private static void WriteResources(BinaryWriter writer, ResourceAmounts values)
        {
            writer.Write(values.Food);
            writer.Write(values.Wood);
            writer.Write(values.Stone);
            writer.Write(values.Iron);
            writer.Write(values.Gold);
        }

        private static void WriteLayout(BinaryWriter writer, LevelLayout layout)
        {
            writer.Write("dark-nights.layout.v2");
            writer.Write(layout.Expedition);
            writer.Write(layout.WorldWidth);
            writer.Write(layout.GroundY);
            writer.Write(layout.BuildMinX);
            writer.Write(layout.BuildMaxX);
            writer.Write(layout.SpawnX);
            // CameraX 仅为本地显示默认值，不影响世界兼容性。
            WritePlacements(writer, layout.Buildings);
            WritePlacements(writer, layout.Worksites);
            WritePlacements(writer, layout.Actors);
            writer.Write(layout.Platforms.Count);
            foreach (var platform in layout.Platforms)
            {
                writer.Write(platform.Id); writer.Write(platform.MinX); writer.Write(platform.MaxX); writer.Write(platform.Height);
            }
        }

        private static void WritePlacements(BinaryWriter writer, IReadOnlyList<PlacementDefinition> entries)
        {
            writer.Write(entries.Count);
            foreach (PlacementDefinition entry in entries)
            {
                writer.Write(entry.Kind);
                writer.Write(entry.X);
                writer.Write(entry.Variant);
                writer.Write(entry.Name);
            }
        }
    }
}
