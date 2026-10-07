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
    /// 地图必须先生成，船体安全停泊后才开放 Ready；普通玩家出生在舱外，飞行中加入者在船内。
    /// </summary>
    internal static class GroundSessionSetup
    {
        internal static void Prepare(ObjectSession world)
        {
            var planet = world.Flow.ActivePlanet ?? world.Flow.Planets[0];
            foreach (var candidate in world.Flow.Planets)
                if (candidate.Enabled) { planet = candidate; break; }
            var journey = world.Journey.Edit();
            journey.JourneyId = Guid.NewGuid().ToString("N");
            journey.PlanetId = planet.Id;
            journey.MapId = world.Terrain.Map.World.WorldId.ToString().Replace("-", "");
            journey.Seed = world.Terrain.Seed;
            world.Journey.SetPhase(JourneyPhase.Descent);
            world.Ship.Arrive(planet.DockX, planet.DockHeight, 0);
            if (!new ShipFlightMotion(world).Land())
                throw new InvalidOperationException("星球地面开局位置不满足飞船安全停泊条件。");
            world.Journey.SetPhase(JourneyPhase.Landed);
        }

        internal static ActorBehaviour SpawnHero(ObjectSession world)
        {
            var ship = world.Expedition.Ship;
            if (ship == null) throw new InvalidOperationException("地面会话缺少保留的飞船。");
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
    }
}
