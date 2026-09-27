using System;
using System.Linq;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Terrain;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>服务端配置边界内飞行及安全着陆；无人驾驶悬停，航程驾驶松手缓降，原坡形碰撞和乘员位移在同一事务推进。</summary>
    internal sealed class ShipFlightMotion
    {
        private readonly ObjectSession world;
        internal ShipFlightMotion(ObjectSession world) { this.world = world; }
        private Core.Config.ShipFlightDefinition Rules => world.Catalog.Balance.Expedition.Ship;
        internal bool Tick(double delta, ActorState pilot)
        {
            var ship = world.Expedition.Ship; var s = ship.Edit();
            if (world.Flow.Enabled && pilot == null)
            { s.ShipVelocityX = s.ShipVelocityY = 0; return false; }
            float dt = (float)delta;
            int horizontal = pilot?.Horizontal ?? 0;
            int vertical = pilot == null ? 0 : (pilot.JumpHeld ? 1 : 0) - (pilot.DropPending ? 1 : 0);
            float targetY = world.Flow.Enabled && pilot != null && vertical == 0 ? -Rules.IdleDescentSpeed : vertical * Rules.VerticalSpeed;
            s.ShipVelocityX = Mathf.MoveTowards(s.ShipVelocityX, horizontal * Rules.HorizontalSpeed, Rules.Acceleration * dt);
            s.ShipVelocityY = Mathf.MoveTowards(s.ShipVelocityY, targetY, Rules.Acceleration * dt);
            // 碰撞后的清零不能把高速接地伪装成满足安全速度，持续加速下降时也必须先松手减速。
            bool safeApproach = targetY <= 0 && Math.Abs(targetY) <= Rules.LandingSpeed &&
                Math.Abs(s.ShipVelocityX) <= Rules.LandingSpeed && Math.Abs(s.ShipVelocityY) <= Rules.LandingSpeed;
            float dx = s.ShipVelocityX * dt, dy = s.ShipVelocityY * dt;
            int count = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)) / 2));
            float oldX = s.X, oldH = s.Height;
            bool touchedGround = false;
            for (int i = 0; i < count; i++)
            {
                float x = s.X + dx / count, h = s.Height + dy / count;
                if (dx != 0 && Clear(x, s.Height)) s.X = x; else s.ShipVelocityX = 0;
                if (dy != 0 && Clear(s.X, h)) s.Height = h;
                else
                {
                    if (dy < 0 && h < s.DockHeight) touchedGround = true;
                    s.ShipVelocityY = 0;
                }
            }
            // 接地路径经过有效泊位时制动，避免单步横移越过容差；安全判定仍使用碰撞前速度。
            float left = s.DockX - Rules.LandingTolerance, right = s.DockX + Rules.LandingTolerance;
            if (world.Flow.Enabled && touchedGround && Math.Min(oldX, s.X) <= right && Math.Max(oldX, s.X) >= left &&
                Clear(s.DockX, s.DockHeight) && Supported(s.DockX, s.DockHeight))
            { s.X = Math.Clamp(s.X, left, right); s.ShipVelocityX = 0; }
            Carry(s.X - oldX, s.Height - oldH);
            return world.Flow.Enabled && pilot != null && safeApproach && Land();
        }

        internal bool Land()
        {
            var s = world.Expedition.Ship.Edit();
            // 本轮仅开放目的地配置产出的安全着陆区，不把任意洞底视为降落点。
            if (Math.Abs(s.X - s.DockX) > Rules.LandingTolerance || s.Height - s.DockHeight > Rules.LandingTolerance || s.Height < s.DockHeight ||
                Math.Abs(s.ShipVelocityX) > Rules.LandingSpeed || Math.Abs(s.ShipVelocityY) > Rules.LandingSpeed) return false;
            if (!Clear(s.DockX, s.DockHeight) || !Supported(s.DockX, s.DockHeight)) return false;
            float dx = s.DockX - s.X, dy = s.DockHeight - s.Height;
            s.X = s.DockX; s.Height = s.DockHeight; s.ShipVelocityX = s.ShipVelocityY = 0;
            Carry(dx, dy); s.ShipPhase = 0; s.ShipDoorClock = 0;
            return true;
        }

        private bool Clear(float x, float h)
        {
            var s = world.Expedition.Ship.Read();
            var planet = world.Flow.Enabled ? world.Flow.ActivePlanet : null;
            float range = planet?.HorizontalRange ?? Rules.HorizontalRange;
            float lift = planet?.MaximumLift ?? Rules.MaximumLift;
            if (Math.Abs(x - s.DockX) > range || h < s.DockHeight || h > s.DockHeight + lift) return false;
            for (float px = -ShipGeometry.HalfWidth; px <= ShipGeometry.HalfWidth; px += 2)
                for (float py = 1; py <= ShipGeometry.Roof; py += 2)
                    if (ShipGeometry.Hull(px, py) && TerrainHeroMotion.Solid(world.Terrain.Map, x + px, h + py)) return false;
            return true;
        }

        private bool Supported(float x, float h)
        {
            if (!TerrainHeroMotion.Solid(world.Terrain.Map, x - 56, h - 1) ||
                !TerrainHeroMotion.Solid(world.Terrain.Map, x + 104, h - 1)) return false;
            for (float local = ShipGeometry.RampToe; local <= ShipGeometry.RampHinge; local += 4)
                for (float head = 1; head <= 28; head += 4)
                    if (TerrainHeroMotion.Solid(world.Terrain.Map, x + local, h + ShipGeometry.Floor(local) + head)) return false;
            return true;
        }

        internal void Carry(float dx, float dh)
        {
            if (dx == 0 && dh == 0) return;
            foreach (var actor in world.Index.Actors.Where(a => a.Read().Boarded))
            { var a = actor.Edit(); a.X += dx; a.Height += dh; }
            foreach (var device in world.Index.Buildings.Where(b => b != world.Expedition.Ship && b.Read().DeviceStage is 0 or 6))
            { var b = device.Edit(); b.X += dx; b.Height += dh; }
        }
    }
}
