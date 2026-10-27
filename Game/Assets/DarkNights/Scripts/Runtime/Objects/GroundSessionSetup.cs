using System;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 星球地面开局及主角出生的准备步骤；只写现有航程、船体和角色状态，不拥有另一份世界。
    /// 新局不创建飞船，在最终自然格形上只读寻找身体净空和支撑；恢复的含船旧档沿用原出生合同。
    /// </summary>
    internal static class GroundSessionSetup
    {
        internal static void Prepare(ObjectSession world)
        {
            if (world.Expedition.Ship == null)
            {
                world.Journey.Edit().Enabled = false;
                world.Journey.SetPhase(JourneyPhase.Landed);
                FindNaturalSpawn(world, out _, out _);
                return;
            }
            var planet = world.Flow.ActivePlanet ?? world.Flow.Planets[0];
            foreach (var candidate in world.Flow.Planets)
                if (candidate.Enabled) { planet = candidate; break; }
            var journey = world.Journey.Edit();
            journey.JourneyId = Guid.NewGuid().ToString("N");
            journey.PlanetId = planet.Id;
            journey.MapId = world.Terrain.Map.World.WorldId.ToString().Replace("-", "");
            journey.Seed = world.Terrain.Seed;
            var node = world.Flow.Navigation.Find(planet.Id);
            journey.StarX = node.X; journey.StarY = node.Y;
            journey.SurveyBits = NavigationSurveyData.Initial(world.Flow.Navigation, node.X, node.Y).Encoded;
            journey.RunId = Guid.NewGuid().ToString("N");
            world.Journey.SetPhase(JourneyPhase.Descent);
            world.Ship.Arrive(planet.DockX, planet.DockHeight, 0);
            if (!new ShipFlightMotion(world).Land())
                throw new InvalidOperationException("星球地面开局位置不满足飞船安全停泊条件。");
            world.Journey.SetPhase(JourneyPhase.Landed);
        }

        internal static ActorBehaviour SpawnHero(ObjectSession world)
        {
            var ship = world.Expedition.Ship;
            if (ship == null)
            {
                FindNaturalSpawn(world, out float x, out float height);
                var resident = world.Lifecycle.SpawnActor("worker", x);
                var ground = resident.Edit();
                ground.ManualControl = true;
                ground.Height = height; ground.VerticalSpeed = 0; ground.SupportPlatform = 0;
                ground.Boarded = false;
                return resident;
            }
            var actor = world.Lifecycle.SpawnActor("worker", ship.X + ShipGeometry.RampToe - 32);
            var state = actor.Edit();
            state.ManualControl = true;
            if (ship.Read().ShipPhase != 0 || world.Flow.Enabled && world.Flow.IsSpace)
            { world.Ship.Cabin.Place(actor); return actor; }
            float origin = ship.X + ShipGeometry.RampToe - 32;
            for (int offset = 0; offset <= 256; offset += PlayableTerrain.CellPixels)
            {
                float x = origin - offset;
                if (x < 16) break;
                if (!TerrainBodyCollision.Ground(world.Terrain.Map, x, ship.Read().Height + 64,
                    ship.Read().Height - 128, HeroControlDefinition.BodyHalfWidth,
                    HeroControlDefinition.BodyHeight, out float height)) continue;
                state.X = state.MoveX = state.RallyX = x;
                state.Height = height; state.VerticalSpeed = 0; state.SupportPlatform = 0;
                state.Boarded = false;
                return actor;
            }
            throw new InvalidOperationException("飞船坡道附近没有安全的主角地面出生点。");
        }

        /// <summary>从左侧内部列依次读取自然地面；不改格子、不补路、不换种子，找不到则明确拒绝开局。</summary>
        internal static void FindNaturalSpawn(ObjectSession world, out float x, out float height)
        {
            for (int column = 2; column < TerrainGenerationSettings.Width - 2; column++)
            {
                x = column * PlayableTerrain.CellPixels;
                if (TerrainBodyCollision.Ground(world.Terrain.Map, x, PlayableTerrain.OriginY,
                    PlayableTerrain.MinimumHeight + PlayableTerrain.CellPixels, HeroControlDefinition.BodyHalfWidth,
                    HeroControlDefinition.BodyHeight, out height) &&
                    height <= world.Catalog.Balance.HeroControl.MaximumHeight) return;
            }
            throw new InvalidOperationException("当前自然地图没有满足人物支撑及净空的出生点。");
        }
    }
}
