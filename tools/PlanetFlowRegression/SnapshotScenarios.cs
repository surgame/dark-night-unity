using System;
using System.IO;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.Save;
using DarkNights.Core.ViewData;
using DarkNights.Tests;

namespace DarkNights.Tools.PlanetFlowRegression
{
    /// <summary>通过正式 SnapshotValidator 总入口检验航程与地图交叉身份；只读实际规则，构造隔离数据而不写玩家存档。</summary>
    internal static class SnapshotScenarios
    {
        private const string World = "33333333333333333333333333333333";
        private static readonly PlanetDefinition Planet = new PlanetDefinition("snapshot", "快照星球");

        public static void Run(ScenarioReport r)
        {
            RuleScenario.RepositoryRoot = Directory.GetCurrentDirectory();
            var catalog = RuleScenario.Catalog();
            var layout = new LevelLayout(5120, 0, 0, 5120, 568, 568,
                new[] { new PlacementDefinition("ship", 568) }, Array.Empty<PlacementDefinition>(),
                Array.Empty<PlacementDefinition>(), randomTerrain: true, expedition: true);
            var space = PlanetTerrainGenerator.Space(World);
            var terrain = PlanetTerrainGenerator.Generate(Planet, "SNAPSHOT-TEST", World);
            foreach (JourneyPhase phase in Enum.GetValues(typeof(JourneyPhase)))
            {
                bool onPlanet = phase is JourneyPhase.ArrivalSync or JourneyPhase.Descent or JourneyPhase.Landed;
                var save = Create(catalog, onPlanet ? terrain : space, phase);
                string error = SnapshotValidator.Validate(save, catalog, layout);
                bool stable = phase is JourneyPhase.Orbit or JourneyPhase.Descent or JourneyPhase.Landed;
                r.Check(stable ? error == "" : error.Contains("切换期间"), phase + "完整存档阶段门控：" + error);
            }
            r.Check(SnapshotValidator.Validate(Create(catalog, terrain, JourneyPhase.Descent, seed: "MISMATCH"), catalog, layout) != "",
                "完整存档拒绝航程实际种子与地图种子不同");
            r.Check(SnapshotValidator.Validate(Create(catalog, terrain, JourneyPhase.Orbit), catalog, layout) != "",
                "完整太空存档拒绝非空星球地图");
            r.Check(SnapshotValidator.Validate(Create(catalog, space, JourneyPhase.Descent), catalog, layout) != "",
                "完整下降存档拒绝空载体环境");
            var high = new PlanetDefinition("high", "高平台", dockRow: 28, maximumLift: 256);
            var highMap = PlanetTerrainGenerator.Generate(high, "SNAPSHOT-HIGH", World);
            var highSave = Create(catalog, highMap, JourneyPhase.Landed, planet: high, crewHeight: 210, boarded: false);
            string highError = SnapshotValidator.Validate(highSave, catalog, layout);
            r.Check(highError == "", "高平台出舱角色完整快照恢复：" + highError);
        }

        private static SessionSnapshot Create(GameCatalog catalog, PlayableTerrain terrain, JourneyPhase phase,
            string seed = null, PlanetDefinition planet = null, float? crewHeight = null, bool boarded = true)
        {
            planet ??= Planet;
            bool orbit = phase == JourneyPhase.Orbit, landed = phase == JourneyPhase.Landed;
            bool onPlanet = phase is JourneyPhase.ArrivalSync or JourneyPhase.Descent or JourneyPhase.Landed;
            float dockX = onPlanet ? planet.DockX : 568;
            float dockHeight = onPlanet ? planet.DockHeight : 0;
            float shipHeight = onPlanet && !landed ? dockHeight + planet.ArrivalHeight : dockHeight;
            var journey = new JourneyViewData(true, orbit ? "" : "44444444444444444444444444444444", 1, phase,
                orbit ? "" : planet.Id, orbit ? "" : seed ?? terrain.Seed, orbit ? "" : World,
                new string('a', 64), 0, "", new[] { planet });
            var actors = new[] { new ActorSnapshot(1, "worker", false, "测试船员", dockX + 96, 1,
                ActorActivity.Idle, 0, dockX + 96, dockX + 96, 1, 0, 0, 0, false, false, 0,
                height: crewHeight ?? shipHeight + 80, manualControl: true) };
            var buildings = new[] { new BuildingSnapshot(2, "ship", dockX, 1, 1, 0, 0, Array.Empty<TrainingSnapshot>()) };
            var crew = new[] { new ExpeditionActorData(1, 0, 0, 0, 0, 0, 0, 0, boarded) };
            var devices = new[] { new ExpeditionDeviceData(2, shipHeight, 0, 0, 0, 0, dockX, dockHeight, true) };
            var ship = new ExpeditionShipData(2, landed ? 0 : 3, orbit || !boarded ? 0 : 1, 0, 0, 0, dockX, dockHeight);
            var expedition = new ExpeditionViewData(1, landed ? 1 : 0, 0, 0, false, 0, 0, 0, 0, 0,
                crew, devices, ship: ship, journey: journey);
            var identities = new[] { new EntityIdentityData(1, "55555555555555555555555555555555", ""),
                new EntityIdentityData(2, "66666666666666666666666666666666", "ship") };
            return new SessionSnapshot(SessionSnapshot.CurrentVersion, catalog.Level.Id,
                new EconomySnapshot(new ResourceAmounts(), 0, 0, 0), new WaveSnapshot(0, WavePhase.Day, 0, 0, 0),
                0, 1, false, 3, "1", "1", actors, buildings, Array.Empty<WorksiteSnapshot>(),
                Array.Empty<ProjectileSnapshot>(), new StatisticsSnapshot(0, 0, new ResourceAmounts()),
                identities: identities, terrain: terrain, expedition: expedition);
        }
    }
}
