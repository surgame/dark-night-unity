using System;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 显式开发快速局的冻结初始条件；只描述星球与种子，不持有第二份运行状态。
    /// 在同一 YYGC 准备事务内写入真实着陆、装备与待接管人物，随后由正常 Ready 接管。
    /// </summary>
    public sealed class QuickTestPreset
    {
        public const string LandedPickaxeId = "landed-pickaxe";
        public const string EmbeddedMineralsId = "landed-embedded-minerals";
        private readonly string id;
        public string Id => id;
        public string Seed => id == EmbeddedMineralsId ? "DN-QUICK-MINERALS-20261004" : "DN-QUICK-PICKAXE-20261002";
        public PlanetDefinition Planet { get; }
        private QuickTestPreset(PlanetDefinition planet, string id) { Planet = planet; this.id = id; }

        public static QuickTestPreset Create(string id, ExpeditionFlowConfig flow)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (id != LandedPickaxeId && id != EmbeddedMineralsId) throw new ArgumentException("未知快速测试项。", nameof(id));
            if (flow == null || !flow.Enabled) throw new InvalidOperationException("快速着陆需要启用正式星球航程配置。");
            return new QuickTestPreset(flow.PreviewPlanet(), id);
#else
            throw new InvalidOperationException("快速测试仅用于 Editor 或开发构建。");
#endif
        }

        internal void Apply(ObjectSession world)
        {
            if (!world.IsExpedition || !world.Flow.Enabled || world.Terrain.Seed != Seed)
                throw new InvalidOperationException("快速测试的正式星球地图尚未准备。");
            var planet = world.Flow.Planets.Single(p => p.Id == Planet.Id && p.Enabled);
            var journey = world.Journey.Edit();
            journey.JourneyId = Guid.NewGuid().ToString("N");
            journey.MapId = world.Terrain.Map.World.WorldId.ToString().Replace("-", "");
            journey.PlanetId = planet.Id; journey.Seed = Seed;
            world.Journey.SetPhase(JourneyPhase.Descent);
            world.Ship.Arrive(planet.DockX, planet.DockHeight, 0);
            if (!new ShipFlightMotion(world).Land())
                throw new InvalidOperationException("当前正式地图的泊位不满足安全着陆条件。");
            world.Flow.Landed();

            var definition = world.Resources.Equipment.MiningDefinitions.SingleOrDefault(d => d.Key == "item.pickaxe")
                ?? throw new InvalidOperationException("快速测试缺少正式矿镐 Definition。");
            var tool = world.Resources.Equipment.Mining(definition.Guid.ToString());
            float x, height, aim = -90;
            if (id == EmbeddedMineralsId) MineralQuickTestSpawn.Find(world, tool, out x, out height, out aim);
            else FindSpawn(world, tool, out x, out height);
            int players = id == EmbeddedMineralsId ? 4 : 1;
            for (int slot = 0; slot < players; slot++)
            {
                var hero = world.Commands.SpawnDefaultResident()
                    ?? throw new InvalidOperationException("快速测试主角装配失败。");
                var state = hero.Edit();
                state.OwnerSlot = slot; state.ManualControl = true; state.Boarded = false;
                state.X = state.MoveX = state.RallyX = x; state.Height = height;
                state.VerticalSpeed = 0; state.SupportPlatform = 0;
                HeroControlBehaviour.ResetInput(state);
                if (!HeroInventoryBehaviour.Give(state, definition.Guid.ToString()))
                    throw new InvalidOperationException("快速测试矿镐装备失败。");
                state.SelectedItem = 0; state.AimAngle = aim;
            }
            world.Notify("矿镐快速测试已准备：已着陆、人在舱外；返回主菜单可启动干净的新测试局。");
        }

        private static void FindSpawn(ObjectSession world, MiningToolRules tool, out float x, out float height)
        {
            var ship = world.Expedition.Ship.Read();
            var map = world.Terrain.Map;
            for (int distance = 192; distance <= 1024; distance += PlayableTerrain.CellPixels)
            {
                float candidateX = ship.X - distance;
                if (candidateX < 32) break;
                if (!TerrainBodyCollision.Ground(map, candidateX, ship.Height + 64, ship.Height - 384,
                    HeroControlDefinition.BodyHalfWidth, HeroControlDefinition.BodyHeight, out float ground)) continue;
                if (!TerrainMiningQuery.FirstSurface(map, candidateX, ground + tool.HandHeight, 0, -1,
                    tool.Reach, out CellCoord target, out _)) continue;
                var cell = map.Read(target).Cell;
                if (TerrainMiningQuery.BlockReason(map, map.Tiles, target).Length != 0 || !map.Rules.CanDamage(cell.TileId) ||
                    tool.BlockReason(HeroMiningTargetKind.Foreground, map.Rules.Material(cell.TileId)).Length != 0) continue;
                x = candidateX; height = ground; return;
            }
            throw new InvalidOperationException("当前配置在泊位附近没有安全且可挥镐的舱外出生点。");
        }
    }
}
