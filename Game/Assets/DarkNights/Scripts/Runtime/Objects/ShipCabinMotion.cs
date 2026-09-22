using System;
using DarkNights.Core.Logic.Terrain;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>玩家与地面工人共用的船内坡道运动；只修改角色唯一位置，不把活动船体烘焙进地形。</summary>
    internal sealed class ShipCabinMotion
    {
        private readonly ObjectSession world;
        internal ShipCabinMotion(ObjectSession world) { this.world = world; }
        private BuildingBehaviour Ship => world.Expedition.Ship;
        internal bool Open => Ship != null && Ship.Read().ShipPhase <= 1;

        internal bool Player(ActorBehaviour actor, double delta)
        {
            if (!world.IsExpedition || Ship == null) return false;
            var s = actor.Edit(); var ship = Ship.Read();
            if (ship.PilotId == actor.Id)
            {
                s.X = ship.X + ShipGeometry.PilotX; s.Height = ship.Height + ShipGeometry.PilotHeight;
                s.Boarded = true; s.Walking = false; s.VerticalSpeed = 0;
                HeroEquipment.Cancel(s); return true;
            }
            float target = s.X + (float)(s.Horizontal * actor.Definition.Speed * world.DebugHeroSpeedMultiplier * delta);
            if (!s.Boarded)
            {
                float toe = ship.X + ShipGeometry.RampToe;
                if (s.X > toe + 8 || s.X < toe - 24) s.ShipEntryBlocked = false;
                if (s.DropPending && Math.Abs(s.X - toe) < 24) s.ShipEntryBlocked = true;
                if (s.ShipEntryBlocked) return false;
                if (!Open || s.Horizontal <= 0 || s.X > toe + 3 || target < toe || Math.Abs(s.Height - ship.Height) > 4) return false;
                s.Boarded = true;
            }
            Walk(actor, target);
            s.JumpPending = s.DropPending = false; HeroEquipment.Cancel(s);
            return true;
        }

        internal void Place(ActorBehaviour actor, float localX = ShipGeometry.HoldX)
        {
            var s = actor.Edit(); s.X = Ship.X + localX; s.Height = Ship.Read().Height + ShipGeometry.Floor(localX);
            s.Boarded = true; s.VerticalSpeed = 0; s.SupportPlatform = 0;
        }

        internal bool Navigate(ActorBehaviour actor, ref float x, ref float height, double delta, out bool arrived)
        {
            arrived = false;
            if (Ship == null || actor.Enemy) return false;
            var s = actor.Edit(); var ship = Ship.Read();
            float localGoal = x - ship.X;
            bool inward = localGoal >= ShipGeometry.RampHinge && localGoal <= ShipGeometry.CabinRight &&
                Math.Abs(height - ship.Height - ShipGeometry.Floor(localGoal)) < 6;
            if (s.Boarded)
            {
                float goal = inward ? x : ship.X + ShipGeometry.RampToe - 8;
                if (!Open && !inward) return true;
                Walk(actor, Mathf.MoveTowards(s.X, goal, (float)(actor.Definition.Speed * delta)));
                arrived = inward && Math.Abs(s.X - x) < 4; return true;
            }
            if (!inward || !Open) return false;
            float toe = ship.X + ShipGeometry.RampToe;
            if (Math.Abs(s.X - toe) < 10 && Math.Abs(s.Height - ship.Height) < 5)
            {
                s.Boarded = true; s.X = toe;
                Walk(actor, Mathf.MoveTowards(s.X, x, (float)(actor.Definition.Speed * delta))); return true;
            }
            x = toe; height = ship.Height; return false;
        }

        private void Walk(ActorBehaviour actor, float target)
        {
            var s = actor.Edit(); var ship = Ship.Read(); float old = s.X;
            float local = target - ship.X;
            if (Open && local < ShipGeometry.RampToe)
            { s.Boarded = false; s.X = target; s.Height = ship.Height; }
            else
            {
                local = Math.Clamp(local, Open ? ShipGeometry.RampToe : ShipGeometry.RampHinge + 8, ShipGeometry.CabinRight);
                s.X = ship.X + local; s.Height = ship.Height + ShipGeometry.Floor(local);
            }
            s.VerticalSpeed = 0; s.SupportPlatform = 0;
            s.Walking = Math.Abs(old - s.X) > .001f;
            if (s.Walking) s.Face = Math.Sign(s.X - old);
        }
    }
}
