using DarkNights.Core.ViewData;
using System;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
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
        internal bool Open => Ship != null && Ship.Read().ShipPhase <= 1 &&
            (!world.Flow.Enabled || world.Flow.Phase == JourneyPhase.Landed && Ship.Read().ShipDoorClock == 0);

        internal bool Player(ActorBehaviour actor, double delta)
        {
            if (!world.IsExpedition || Ship == null) return false;
            var s = actor.Edit(); var ship = Ship.Read();
            if (ship.PilotId == actor.Id)
            {
                s.X = ship.X + ShipGeometry.PilotX; s.Height = ship.Height + ShipGeometry.PilotHeight;
                s.Boarded = true; s.Walking = false; s.VerticalSpeed = 0; s.JumpPending = false;
                HeroEquipment.Cancel(s); return true;
            }
            float target = s.X + s.Horizontal * HeroControlBehaviour.PlayerMoveSpeed(actor) * (float)delta;
            if (!s.Boarded)
            {
                // 交界处运动适配：入口仍以 RampToe 和脚底高度判定；修改登船方式时须与 ShipRampTransition 一起检查。
                float toe = ship.X + ShipGeometry.RampToe;
                if (s.X > toe + 8 || s.X < toe - 24) s.ShipEntryBlocked = false;
                if (s.DropPending && Math.Abs(s.X - toe) <= 24) s.ShipEntryBlocked = true;
                if (s.ShipEntryBlocked) return false;
                if (!Open || s.Horizontal <= 0 || s.X > toe + 3 || target < toe || Math.Abs(s.Height - ship.Height) > 4) return false;
                s.Boarded = true;
            }
            MovePlayer(actor, target, delta);
            return true;
        }

        private void MovePlayer(ActorBehaviour actor, float target, double delta)
        {
            var s = actor.Edit(); var ship = Ship.Read();
            float oldLocal = Math.Clamp(s.X - ship.X, ShipGeometry.RampToe, ShipGeometry.CabinRight);
            float requestedLocal = target - ship.X;
            float local = Math.Clamp(requestedLocal, Open ? ShipGeometry.RampToe : ShipGeometry.RampHinge + 8,
                ShipGeometry.CabinRight);
            float oldFloor = ship.Height + ShipGeometry.Floor(oldLocal);
            bool grounded = s.VerticalSpeed <= 0 && s.Height <= oldFloor + .5f;

            // 交界处运动适配的唯一调用点：移除 ShipRampTransition 时可恢复下方原有步行出舱路径。
            bool adapted = false, leftCabin = false;
            adapted = ShipRampTransition.TryLeave(actor, ship.X, target, Open, delta, ref grounded, out leftCabin);
            if (leftCabin) return;
            if (!adapted && Open && requestedLocal < ShipGeometry.RampToe && grounded && !s.JumpPending)
            {
                float exitX = s.X;
                s.Boarded = false; s.X = target; s.Height = ship.Height;
                s.VerticalSpeed = 0; s.SupportPlatform = -1;
                s.Walking = Math.Abs(s.X - exitX) > .001f;
                s.JumpPending = s.DropPending = false; HeroEquipment.Cancel(s);
                return;
            }

            if (local < ShipGeometry.RampToe) local = ShipGeometry.RampToe;
            float maximumFoot = MaximumFootHeight(local, s.ManualControl);
            if (s.Height - ship.Height > maximumFoot)
            {
                local = oldLocal;
                maximumFoot = MaximumFootHeight(local, s.ManualControl);
            }

            float previousX = s.X;
            s.X = ship.X + local;
            s.Walking = Math.Abs(previousX - s.X) > .001f;
            if (s.Walking) s.Face = Math.Sign(s.X - previousX);
            float floor = ship.Height + ShipGeometry.Floor(local);
            if (grounded) s.Height = floor;

            HeroControlDefinition rules = world.Catalog.Balance.HeroControl;
            if (grounded && s.JumpPending && rules != null)
            {
                s.VerticalSpeed = rules.JumpSpeed;
                s.SupportPlatform = -1; grounded = false;
            }
            s.JumpPending = s.DropPending = false;
            HeroEquipment.Cancel(s);
            if (grounded)
            {
                s.Height = floor; s.VerticalSpeed = 0; s.SupportPlatform = 0;
                if (s.JetpackOwned && rules != null)
                    s.JetpackFuel = Math.Min(rules.FuelSeconds, s.JetpackFuel + rules.FuelRecovery * delta);
                return;
            }

            float previousHeight = s.Height;
            s.VerticalSpeed -= (float)(rules?.Gravity ?? 0) * (float)delta;
            float nextHeight = previousHeight + s.VerticalSpeed * (float)delta;
            if (nextHeight - ship.Height > maximumFoot)
            {
                nextHeight = ship.Height + maximumFoot;
                if (s.VerticalSpeed > 0) s.VerticalSpeed = 0;
            }
            if (s.VerticalSpeed <= 0 && nextHeight <= floor)
            {
                s.Height = floor; s.VerticalSpeed = 0; s.SupportPlatform = 0;
            }
            else
            {
                s.Height = nextHeight; s.SupportPlatform = -1;
            }
        }

        private static float MaximumFootHeight(float local, bool manual)
        {
            float halfWidth = manual ? HeroControlDefinition.BodyHalfWidth : 5;
            float height = manual ? HeroControlDefinition.BodyHeight : 22;
            return Math.Min(ShipGeometry.CabinCeiling(local - halfWidth),
                Math.Min(ShipGeometry.CabinCeiling(local), ShipGeometry.CabinCeiling(local + halfWidth))) - height;
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
            // 自动工人沿用任务导航；ShipRampTransition 只适配玩家跳跃出口。改变通用登船路线时需同时检查此分支。
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
