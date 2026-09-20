using System;
using System.Collections.Generic;
using AnyRules.Next;
using DarkNights.Runtime.Terrain;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>有界二维路径缓存；只读权威碰撞，地形提交后失效，位置仍由角色 State 与扫掠运动拥有。</summary>
    internal sealed class ExpeditionNavigation
    {
        private readonly ObjectSession world;
        private readonly Dictionary<int, Queue<Vector2>> paths = new Dictionary<int, Queue<Vector2>>();
        private readonly Dictionary<int, Vector2> goals = new Dictionary<int, Vector2>();
        private ulong commit;
        private object mapIdentity;
        private readonly Dictionary<int, (Vector2 Position, double Time)> progress = new Dictionary<int, (Vector2, double)>();
        internal ExpeditionNavigation(ObjectSession world) { this.world = world; }
        internal static bool Sight(IReadOnlyGrid map, float ax, float ay, float bx, float by)
        {
            int n = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Pow(bx - ax, 2) + Math.Pow(by - ay, 2)) / 2));
            for (int i = 1; i <= n; i++)
                if (TerrainHeroMotion.Solid(map, ax + (bx - ax) * i / n, ay + (by - ay) * i / n)) return false;
            return true;
        }
        internal bool Move(ActorBehaviour actor, float x, float height, double delta, bool flying)
        {
            var map = world.Terrain.Map; var s = actor.Edit(); var goal = new Vector2(x, height);
            if (mapIdentity != map || commit != map.CommitId)
            { paths.Clear(); goals.Clear(); progress.Clear(); mapIdentity = map; commit = map.CommitId; }
            if (Vector2.Distance(new Vector2(s.X, s.Height), goal) < 20) return true;
            var position = new Vector2(s.X, s.Height);
            if (!progress.TryGetValue(actor.Id, out var sample) || Vector2.Distance(position, sample.Position) > 4)
                progress[actor.Id] = (position, 0);
            else
            {
                progress[actor.Id] = (sample.Position, sample.Time + delta);
                if (sample.Time >= 3)
                {
                    goals.Remove(actor.Id); progress[actor.Id] = (position, 0);
                    world.Notify("路径受阻，正在重新寻找通路；可开路或召回。", true);
                }
            }
            if (!goals.TryGetValue(actor.Id, out var old) || Vector2.Distance(goal, old) > 20)
            { goals[actor.Id] = goal; paths[actor.Id] = Find(new Vector2(s.X, s.Height), goal, flying); }
            if (!paths.TryGetValue(actor.Id, out var path) || path.Count == 0)
            {
                return false;
            }
            var next = path.Peek();
            if (Vector2.Distance(new Vector2(s.X, s.Height), next) < 12)
            { path.Dequeue(); return false; }
            float step = (float)(Math.Max(30, actor.Definition.Speed) * delta);
            if (Math.Abs(next.x - s.X) > .01f) s.Face = Math.Sign(next.x - s.X);
            s.Walking = true;
            if (flying)
            {
                Vector2 to = Vector2.MoveTowards(new Vector2(s.X, s.Height), next, step * 2);
                if (Clear(to.x, to.y, true)) { s.X = to.x; s.Height = to.y; }
                else { paths.Remove(actor.Id); goals.Remove(actor.Id); }
            }
            else
            {
                float previous = s.X;
                TerrainHeroMotion.MoveHorizontal(map, s, Mathf.MoveTowards(s.X, next.x, step));
                bool jump = next.y > s.Height + 2 || Math.Abs(previous - s.X) < .01f;
                TerrainHeroMotion.Tick(map, s, world.Catalog.Balance.HeroControl, delta, jump, false);
            }
            return false;
        }

        internal bool CanReach(ActorBehaviour actor, float x, float height, bool flying) =>
            Find(new Vector2(actor.X, actor.Read().Height), new Vector2(x, height), flying).Count > 0;

        private Queue<Vector2> Find(Vector2 start, Vector2 goal, bool flying)
        {
            const int step = 8, width = 640;
            var queue = new Queue<int>(); var previous = new Dictionary<int, int>(); var positions = new Dictionary<int, Vector2>();
            int Key(Vector2 v) => Mathf.RoundToInt(v.x / step) + (Mathf.RoundToInt((v.y + 2560) / step)) * width;
            int first = Key(start), found = -1; queue.Enqueue(first); previous[first] = first; positions[first] = start;
            while (queue.Count > 0 && previous.Count < 18000)
            {
                int current = queue.Dequeue(); Vector2 p = positions[current];
                if (Vector2.Distance(p, goal) < 20) { found = current; break; }
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && (!flying || dy == 0)) continue;
                    var next = p + new Vector2(dx * step, dy * step);
                    if (!flying)
                    {
                        if (dy != 0 || !Floor(next.x, p.y, out float floor)) continue;
                        next.y = floor;
                    }
                    if (next.x < 16 || next.x > 5100 || next.y < -2400 || next.y > 128 || !Clear(next.x, next.y, flying)) continue;
                    float travel = flying ? 10 : Math.Max(p.y, next.y) - Math.Min(p.y, next.y) + 24;
                    if (!Sight(world.Terrain.Map, p.x, p.y + travel, next.x, next.y + travel)) continue;
                    int key = Key(next); if (previous.ContainsKey(key)) continue;
                    previous[key] = current; positions[key] = next; queue.Enqueue(key);
                }
            }
            var result = new Queue<Vector2>(); if (found < 0) return result;
            var reverse = new List<Vector2>();
            while (found != first) { reverse.Add(positions[found]); found = previous[found]; }
            reverse.Reverse(); foreach (var p in reverse) result.Enqueue(p);
            result.Enqueue(goal); return result;
        }
        private bool Floor(float x, float height, out float floor)
        {
            for (float h = height + 18; h >= height - 24; h -= 1)
                if ((TerrainHeroMotion.Solid(world.Terrain.Map, x - 5, h - .5f) ||
                    TerrainHeroMotion.Solid(world.Terrain.Map, x, h - .5f) ||
                    TerrainHeroMotion.Solid(world.Terrain.Map, x + 5, h - .5f)) && Clear(x, h, false))
                { floor = h; return true; }
            floor = 0; return false;
        }
        private bool Clear(float x, float h, bool flying)
        {
            for (float y = 1; y <= (flying ? 14 : 22); y += 3)
                if (TerrainHeroMotion.Solid(world.Terrain.Map, x - 5, h + y) ||
                    TerrainHeroMotion.Solid(world.Terrain.Map, x + 5, h + y)) return false;
            return true;
        }
    }
}
