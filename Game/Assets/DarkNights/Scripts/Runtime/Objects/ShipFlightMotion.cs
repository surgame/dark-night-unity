using System;
using System.Linq;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Terrain;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>服务端配置边界内悬停飞行；两单位子步查询原坡形，并把位移同事务施加给乘员和已收纳设备。</summary>
    internal sealed class ShipFlightMotion
    {
        private readonly ObjectSession world;
        internal ShipFlightMotion(ObjectSession world) { this.world = world; }
        private Core.Config.ShipFlightDefinition Rules => world.Catalog.Balance.Expedition.Ship;
        internal void Tick(double delta, ActorState pilot)
        {
            var ship = world.Expedition.Ship; var s = ship.Edit();
            float dt = (float)delta;
            int horizontal = pilot?.Horizontal ?? 0;
            int vertical = pilot == null ? 0 : (pilot.JumpHeld ? 1 : 0) - (pilot.DropPending ? 1 : 0);
            // 无输入时悬停制动；过期输入、失焦和断线由同一租约路径清零。
            s.ShipVelocityX = Mathf.MoveTowards(s.ShipVelocityX, horizontal * Rules.HorizontalSpeed, Rules.Acceleration * dt);
            s.ShipVelocityY = Mathf.MoveTowards(s.ShipVelocityY, vertical * Rules.VerticalSpeed, Rules.Acceleration * dt);
            float dx = s.ShipVelocityX * dt, dy = s.ShipVelocityY * dt;
            int count = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)) / 2));
            float oldX = s.X, oldH = s.Height;
            for (int i = 0; i < count; i++)
            {
                float x = s.X + dx / count, h = s.Height + dy / count;
                if (dx != 0 && Clear(x, s.Height)) s.X = x; else s.ShipVelocityX = 0;
                if (dy != 0 && Clear(s.X, h)) s.Height = h; else s.ShipVelocityY = 0;
            }
            Carry(s.X - oldX, s.Height - oldH);
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
