using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Experiments.MiningExploration
{
    /// <summary>
    /// R1工具的本地只读输入边界；复用已保存WorldSession星球参数和正式Core生成入口。
    /// 当前仅支持已核对的单个禁用入口步骤，配置变化即拒绝，避免静默套另一组默认参数。
    /// </summary>
    public sealed class LocalTerrainInput
    {
        public string ConfigPath { get; }
        public string MapPath { get; }
        public string Seed { get; }
        public MineMapDefinition Map { get; }
        public PlayableTerrain Terrain { get; }

        public LocalTerrainInput(string project, string mapId)
        {
            ConfigPath = Path.Combine(project, "Game/Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset");
            MapPath = Path.Combine(project, "Game/Assets/DarkNights/Res/Terrain/MineMaps/" + mapId + ".minemap");
            string config = File.ReadAllText(ConfigPath);
            string cave = Regex.Match(config, @"(?s)CaveMap:\s*\r?\n(.*?)\s*MineMap:").Groups[1].Value;
            string planet = Regex.Match(config, @"(?s)- Id: grey-pine\s*\r?\n(.*?)\s*ModifierSchemaVersion:").Groups[1].Value;
            string modifiers = Regex.Match(config, @"(?s)        Modifiers:\s*\r?\n(.*?)(?=\r?\n    - rid:)").Groups[1].Value;
            if (Regex.Matches(modifiers, "- rid:").Count != 1 ||
                !Regex.IsMatch(config, @"(?s)class: EntranceWalkwayModifierConfig.*?Enabled: 0"))
                throw new InvalidOperationException("正式地形步骤已变化，需重新适配冻结输入。");
            Map = JsonSerializer.Deserialize<MineMapDefinition>(File.ReadAllText(MapPath),
                new JsonSerializerOptions { IncludeFields = true }).CopyValidated();
            Seed = Value(cave, "Seed");
            var settings = new TerrainGenerationSettings
            {
                Seed = Seed, Surface = Value(cave, "Surface"), OrganicCaves = Value(cave, "OrganicCaves") == "1",
                Amplitude = Number(cave, "Amplitude"), OreDensity = Number(cave, "OreDensity"),
                ResourceProfile = Value(cave, "ResourceProfile"), MineMap = Map
            };
            var definition = new PlanetDefinition("grey-pine", "灰松星", dockColumn: (int)Number(planet, "DockColumn"),
                dockRow: (int)Number(planet, "DockRow"), landingWidth: (int)Number(planet, "LandingWidth"),
                arrivalHeight: (float)Number(planet, "ArrivalHeight"), horizontalRange: (float)Number(planet, "HorizontalRange"),
                maximumLift: (float)Number(planet, "MaximumLift"), transitSeconds: (float)Number(planet, "TransitSeconds"),
                starCount: (int)Number(planet, "StarCount"), starSpeed: (float)Number(planet, "StarSpeed"),
                transitionKind: Value(planet, "TransitionKind"), spaceColorHex: Value(planet, "SpaceColorHex").Trim('\''),
                skyColorHex: Value(planet, "SkyColorHex").Trim('\''));
            Terrain = PlanetTerrainGenerator.GenerateCandidate(definition, Seed,
                "00000000000000000000000000000002", settings,
                pipeline: new TerrainGenerationPipeline(Array.Empty<ITerrainGenerationModifier>()));
        }

        private static string Value(string text, string name)
        {
            var match = Regex.Match(text, @"(?m)^\s*" + name + @":\s*([^\r\n]*)$");
            if (!match.Success) throw new InvalidOperationException("缺少正式配置字段：" + name);
            return match.Groups[1].Value.Trim();
        }

        private static double Number(string text, string name) => double.Parse(Value(text, name), CultureInfo.InvariantCulture);
    }
}
