using System;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Terrain;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>机器人舱配套侦察照明无人机；从顶舱口逐段离船，在泊位附近巡检，召回沿原口返回且不穿墙。</summary>
    internal sealed class ExpeditionDrone
    {
        private readonly ObjectSession world;
        internal ExpeditionDrone(ObjectSession world) { this.world = world; }
        internal void Tick(ActorBehaviour actor, double delta)
        {
            var s = actor.Edit(); var ship = world.Expedition.Ship.Read();
            bool recall = world.Ship.Recalling || ship.ShipPhase == 3;
            s.Walking = false; s.ActionTime += delta;
            // 0 机库，1 垂直离舱，2 外部巡检，3 返回口上方，4 入舱。
            if (s.TaskPhase == 0)
            {
                s.Boarded = true; s.X = ship.X + ShipGeometry.HatchX; s.Height = ship.Height + 84;
                if (!recall && world.Camp.Read().ExpeditionPhase == 1) { s.TaskPhase = 1; s.Boarded = false; }
                return;
            }
            if (recall && s.TaskPhase is 1 or 2) s.TaskPhase = 3;
            float x = ship.X + ShipGeometry.HatchX, h = ship.Height + ShipGeometry.HatchHeight;
            if (s.TaskPhase == 2)
            {
                s.TaskClock += delta;
                x += (float)Math.Sin(s.TaskClock * .35) * 80; h += 24;
            }
            if (s.TaskPhase == 4) h = ship.Height + 84;
            bool arrived = Move(s, x, h, (float)(world.Catalog.Balance.Units[actor.RuleKey].Speed * delta));
            if (!arrived) return;
            if (s.TaskPhase == 1) { s.TaskPhase = 2; s.Boarded = false; }
            else if (s.TaskPhase == 3) s.TaskPhase = 4;
            else if (s.TaskPhase == 4) { s.TaskPhase = 0; s.Boarded = true; }
        }

        private bool Move(ActorState s, float x, float h, float step)
        {
            var target = new Vector2(x, h); var p = new Vector2(s.X, s.Height);
            int count = Math.Max(1, (int)Math.Ceiling(step / 2));
            for (int i = 0; i < count; i++)
            {
                var next = Vector2.MoveTowards(p, target, step / count);
                for (float dx = -12; dx <= 12; dx += 4)
                    for (float dy = -12; dy <= 12; dy += 3)
                        if (TerrainHeroMotion.Solid(world.Terrain.Map, next.x + dx, next.y + dy)) return false;
                p = next;
            }
            if (Math.Abs(p.x - s.X) > .01) s.Face = Math.Sign(p.x - s.X);
            s.X = p.x; s.Height = p.y; s.Walking = true;
            return Vector2.Distance(p, target) < 1;
        }
    }
}
