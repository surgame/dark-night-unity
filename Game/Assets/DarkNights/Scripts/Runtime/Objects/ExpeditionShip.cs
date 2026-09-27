using DarkNights.Core.ViewData;
using System;
using System.Linq;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>飞船驾驶席和舱门阶段协调；0 停泊（可含开门倒计时）、1 召回、2 关舱、3 飞行，权威字段只归 BuildingState。</summary>
    public sealed class ExpeditionShip
    {
        private readonly ObjectSession world;
        internal ShipCabinMotion Cabin { get; }
        private readonly ShipFlightMotion flight;
        internal ExpeditionShip(ObjectSession world)
        { this.world = world; Cabin = new ShipCabinMotion(world); flight = new ShipFlightMotion(world); }
        internal bool Recalling => world.Expedition.Ship?.Read().ShipPhase is 1 or 2 || world.Camp.Read().ExpeditionPhase >= 2;
        internal bool Docked => world.Expedition.Ship?.Read().ShipPhase == 0;
        private BuildingBehaviour Ship => world.Expedition.Ship;

        internal int Command(string operation, ActorBehaviour hero)
        {
            if (Ship == null || hero == null || hero.Hp <= 0 || world.Camp.Read().ExpeditionPhase == 3) return 0;
            var s = Ship.Edit(); var a = hero.Edit();
            switch (operation)
            {
                case "pilot":
                    if (world.Flow.Enabled && world.Flow.Phase is not (JourneyPhase.Descent or JourneyPhase.Landed)) return 0;
                    if (s.PilotId == hero.Id)
                    {
                        if (s.ShipPhase != 0 && (!world.Flow.Enabled || world.Flow.Phase != JourneyPhase.Descent)) return 0;
                        s.ShipVelocityX = s.ShipVelocityY = 0;
                        s.PilotId = 0; a.ControlLease = checked(a.ControlLease + 1); HeroControlBehaviour.ResetInput(a); return 1;
                    }
                    if (s.PilotId != 0 || !a.Boarded || !ShipGeometry.AtPilot(a.X - s.X, a.Height - s.Height)) return 0;
                    s.PilotId = hero.Id; a.ControlLease = checked(a.ControlLease + 1); HeroControlBehaviour.ResetInput(a);
                    world.Notify(world.Flow.Enabled ? "已进入驾驶位：A/D 平移，空格上升，S 加速下降；松手缓降，对准泊位后自动着陆。" :
                        "已进入驾驶位：A/D 平移，空格上升，S 下降；松开悬停，低速接近降落区后着陆。"); return 1;
                case "takeoff":
                    if (world.Flow.Enabled) return 0;
                    if (s.PilotId != hero.Id || s.ShipPhase != 0 || world.Camp.Read().ExpeditionPhase is not (0 or 1 or 4)) return 0;
                    s.ShipPhase = 1; s.ShipDoorClock = 0;
                    world.Notify("收舱中：工人、机器人、无人机及设备归队后才会关闭坡道。玩家请走入舱内。"); return 1;
                case "cancel-flight":
                    if (s.PilotId != hero.Id || s.ShipPhase is not (1 or 2)) return 0;
                    s.ShipPhase = 0; s.ShipDoorClock = 0; return 1;
                case "land":
                    if (world.Flow.Enabled) return 0;
                    if (s.PilotId != hero.Id || s.ShipPhase != 3 || !flight.Land()) return 0;
                    world.Flow.Landed();
                    world.Notify("着陆完成，坡道已展开；可离开驾驶位步行下船。"); return 1;
                case "deploy":
                    if (s.PilotId != hero.Id || !Docked || world.Camp.Read().ExpeditionPhase != 1) return 0;
                    world.ExpeditionDevices.BeginDeployment(); return 1;
                default: return 0;
            }
        }

        internal void Tick(double delta)
        {
            if (Ship == null) return;
            var s = Ship.Edit();
            var pilot = world.Index.Find<ActorBehaviour>(s.PilotId);
            if (pilot == null || pilot.Hp <= 0 || !pilot.Read().Boarded || pilot.Read().ControllerSlot < 0)
            {
                s.PilotId = 0;
                if (world.Flow.Enabled) s.ShipVelocityX = s.ShipVelocityY = 0;
                if (s.ShipPhase is 1 or 2) { s.ShipPhase = 0; s.ShipDoorClock = 0; }
                pilot = null;
            }
            if (s.ShipPhase == 0 && s.ShipDoorClock > 0)
            {
                s.ShipDoorClock = Math.Max(0, s.ShipDoorClock - delta);
                if (s.ShipDoorClock == 0) world.Notify("坡道已展开，可从左侧步行下船。");
            }
            if (s.ShipPhase == 1 && Ready()) { s.ShipPhase = 2; s.ShipDoorClock = world.Catalog.Balance.Expedition.Ship.DoorSeconds; }
            if (s.ShipPhase == 2)
            {
                s.ShipDoorClock = Math.Max(0, s.ShipDoorClock - delta);
                if (s.ShipDoorClock == 0) s.ShipPhase = 3;
            }
            if (s.ShipPhase == 3)
            {
                if (!world.Flow.Enabled || world.Flow.Phase == JourneyPhase.Descent)
                {
                    if (flight.Tick(delta, pilot?.Read())) CompleteLanding(pilot);
                }
                else s.ShipVelocityX = s.ShipVelocityY = 0;
            }
        }

        private void CompleteLanding(ActorBehaviour pilot)
        {
            var s = Ship.Edit(); s.PilotId = 0;
            s.ShipDoorClock = world.Catalog.Balance.Expedition.Ship.DoorSeconds;
            var a = pilot.Edit(); a.ControlLease = checked(a.ControlLease + 1); HeroControlBehaviour.ResetInput(a);
            world.Flow.Landed();
            world.Notify("已安全着陆并离开驾驶位，正在展开坡道。");
        }

        private bool Ready()
        {
            var s = Ship.Read();
            if (world.Index.Buildings.Any(b => b != Ship && b.Read().DeviceStage is not (0 or 6))) return false;
            return world.Index.Actors.Where(a => !a.Enemy && a.Hp > 0).All(a => a.Read().Boarded &&
                (a.Read().ExpeditionRole == 4 || ShipGeometry.Inside(a.X - s.X, a.Read().Height - s.Height)));
        }

        internal void ReleasePilot(int actorId)
        {
            if (Ship == null || Ship.Read().PilotId != actorId) return;
            if (world.Flow.Enabled && world.Flow.Phase is JourneyPhase.Preparing or JourneyPhase.Transit)
            { world.Flow.ArrivalFailed("驾驶者已离线，航程已取消。"); return; }
            var s = Ship.Edit(); s.PilotId = 0; s.ShipVelocityX = s.ShipVelocityY = 0;
            if (s.ShipPhase is 1 or 2) { s.ShipPhase = 0; s.ShipDoorClock = 0; }
        }

        internal void ResetDock()
        {
            var s = Ship.Edit(); s.X = s.DockX; s.Height = s.DockHeight;
            s.PilotId = s.ShipPhase = 0; s.ShipVelocityX = s.ShipVelocityY = 0; s.ShipDoorClock = 0;
            foreach (var a in world.Index.Actors.Where(a => !a.Enemy))
            { Cabin.Place(a); HeroControlBehaviour.ResetInput(a.Edit()); }
        }

        internal void Arrive(float dockX, float dockHeight, float arrivalHeight)
        {
            var s = Ship.Edit();
            float x = dockX, h = dockHeight + arrivalHeight;
            flight.Carry(x - s.X, h - s.Height);
            s.X = x; s.Height = h; s.DockX = dockX; s.DockHeight = dockHeight;
            s.ShipPhase = 3; s.ShipDoorClock = 0; s.ShipVelocityX = s.ShipVelocityY = 0;
            foreach (var actor in world.Index.Actors.Where(a => a.Read().Boarded))
                HeroControlBehaviour.ResetInput(actor.Edit());
        }
    }
}
