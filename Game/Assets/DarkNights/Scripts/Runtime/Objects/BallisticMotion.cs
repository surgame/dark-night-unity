using System;
using System.Linq;
using DarkNights.Runtime.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 权威投射物的固定步推进与扫掠碰撞；每段不超过两个像素，查询逻辑地形和敌军能力。
    /// 不使用 Physics 回调、客户端 Collider 或网络命令；所有写入处于所属会话事务中。
    /// </summary>
    internal static class BallisticMotion
    {
        internal static bool Solid(ObjectSession world, float x, float height, float radius)
        {
            if (x - radius < 0 || x + radius > world.Layout.WorldWidth) return true;
            if (world.Terrain == null) return height - radius <= 0;
            return TerrainHeroMotion.Solid(world.Terrain.Map, x - radius, height) ||
                TerrainHeroMotion.Solid(world.Terrain.Map, x + radius, height) ||
                TerrainHeroMotion.Solid(world.Terrain.Map, x, height - radius) ||
                TerrainHeroMotion.Solid(world.Terrain.Map, x, height + radius);
        }

        internal static void Tick(ObjectSession world, ref BallisticFlight shot, double delta, HandheldConfig config)
        {
            if (shot.Kind == 0) return;
            shot.Age += delta;
            if (shot.Age >= shot.Lifetime)
            {
                if (shot.Kind == 2) Explode(world, ref shot, config);
                else shot = default;
                return;
            }
            if (shot.Stuck || shot.Kind == 3) return;
            float seconds = (float)delta;
            float dx = shot.VelocityX * seconds;
            float dy = shot.VelocityY * seconds - shot.Gravity * seconds * seconds * .5f;
            shot.VelocityY -= shot.Gravity * seconds;
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / 2));
            for (int i = 0; i < steps; i++)
            {
                float x = shot.X + dx / steps, h = shot.Height + dy / steps;
                if (Solid(world, x, h, shot.Radius))
                {
                    if (shot.Kind == 1) shot = default;
                    else { shot.Stuck = true; shot.VelocityX = shot.VelocityY = 0; }
                    return;
                }
                shot.X = x; shot.Height = h;
                if (shot.Kind != 1) continue;
                ActorBehaviour hit = null;
                foreach (var actor in world.Index.Actors)
                {
                    if (!actor.Enemy || actor.Hp <= 0 || Math.Abs(actor.X - x) > 5 + shot.Radius ||
                        h < actor.Read().Height - shot.Radius || h > actor.Read().Height + 18 + shot.Radius) continue;
                    if (hit == null || actor.Id < hit.Id) hit = actor;
                }
                if (hit == null) continue;
                int damage = shot.Damage;
                shot = default;
                world.Combat.Damage(hit, damage);
                return;
            }
        }

        private static void Explode(ObjectSession world, ref BallisticFlight shot, HandheldConfig config)
        {
            float x = shot.X, h = shot.Height, radius = shot.BlastRadius;
            int damage = shot.Damage;
            shot.Kind = 3; shot.Age = 0; shot.Lifetime = config.ExplosionSeconds;
            shot.VelocityX = shot.VelocityY = shot.Gravity = 0; shot.Stuck = true; shot.Damage = 0;
            // 冻结候选引用，死亡可能立即从索引删除；射线阻挡避免爆炸隔墙伤害。
            foreach (var actor in world.Index.Actors.ToArray())
            {
                float dx = actor.X - x, dy = actor.Read().Height + 9 - h;
                if (!actor.Enemy || actor.Hp <= 0 || dx * dx + dy * dy > radius * radius) continue;
                int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / 2));
                bool blocked = false;
                for (int i = 1; i < steps; i++)
                    if (Solid(world, x + dx * i / steps, h + dy * i / steps, 0)) { blocked = true; break; }
                if (!blocked) world.Combat.Damage(actor, damage);
            }
        }
    }
}
