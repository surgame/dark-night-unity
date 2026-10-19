using System;
using System.Linq;
using DarkNights.Core.ViewData;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>自动集合、升空和降落步骤；不拥有阶段，所有写入发生在既有会话模拟事务内。</summary>
    internal sealed class ExpeditionFlightLifecycle
    {
        private readonly ObjectSession world;
        internal ExpeditionFlightLifecycle(ObjectSession world) { this.world = world; }

        internal void Tick(double delta)
        {
            var ship = world.Expedition.Ship.Edit();
            var journey = world.Journey.Edit();
            ship.PilotId = 0; ship.ShipVelocityX = ship.ShipVelocityY = 0;
            if (journey.Phase == JourneyPhase.Gathering)
            {
                bool ready = world.Index.Actors.Where(a => !a.Enemy).All(a => a.Hp > 0 && a.Read().Boarded &&
                    ShipGeometry.Inside(a.X - ship.X, a.Read().Height - ship.Height));
                if (!ready || !new ShipCargoService(world).CanSettle)
                { journey.PhaseElapsed = 0; ship.ShipPhase = 1; ship.ShipDoorClock = 0; return; }
                ship.ShipPhase = 2;
                ship.ShipDoorClock = Math.Max(0, world.Catalog.Balance.Expedition.Ship.DoorSeconds - journey.PhaseElapsed);
                if (ship.ShipDoorClock == 0)
                { ship.ShipPhase = 3; world.Journey.SetPhase(JourneyPhase.Ascent); world.Notify("全员登船，正在自动升空。"); }
                return;
            }
            if (journey.Phase == JourneyPhase.Ascent)
            {
                float height = Math.Min(ship.DockHeight + world.Flow.ActivePlanet.ArrivalHeight,
                    ship.Height + world.Catalog.Balance.Expedition.Ship.VerticalSpeed * (float)delta);
                new ShipFlightMotion(world).Carry(0, height - ship.Height); ship.Height = height;
                return;
            }
            if (journey.Phase == JourneyPhase.Descent)
            {
                float duration = Math.Max(1, world.Flow.Navigation.DescentSeconds);
                float next = ship.DockHeight + world.Flow.ActivePlanet.ArrivalHeight *
                    (float)Math.Max(0, 1 - journey.PhaseElapsed / duration);
                new ShipFlightMotion(world).Carry(0, next - ship.Height); ship.Height = next;
                if (journey.PhaseElapsed >= duration)
                {
                    if (!new ShipFlightMotion(world).Land())
                    { journey.Error = "审核泊位无法安全着陆，保持舱门关闭。"; return; }
                    ship.ShipDoorClock = world.Catalog.Balance.Expedition.Ship.DoorSeconds;
                    world.Flow.Landed(); world.Notify("已安全着陆，正在展开坡道。");
                }
                return;
            }
            if (journey.Phase == JourneyPhase.Landed && ship.ShipDoorClock > 0)
                ship.ShipDoorClock = Math.Max(0, ship.ShipDoorClock - delta);
        }
    }
}
