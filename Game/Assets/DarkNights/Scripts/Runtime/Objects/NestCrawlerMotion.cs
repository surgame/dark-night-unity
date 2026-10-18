using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>幼虫有限地面追近与重力；用同一权威地形查询身体净空，不穿墙、不跳跃、不触发角色HP变化。</summary>
    internal static class NestCrawlerMotion
    {
        internal static void Tick(ObjectSession world, TerrainNest nest, ref NestCrawlerState state, double delta)
        {
            float x = state.X, h = state.Height;
            var target = world.Index.Actors.Where(a => a.Read().ManualControl && !a.Read().Boarded && a.Hp > 0 &&
                Math.Abs(a.X - x) < 224 && Math.Abs(a.Read().Height - h) < 64).OrderBy(a => Math.Abs(a.X - x)).ThenBy(a => a.Id).FirstOrDefault();
            float dx = target == null || Math.Abs(target.X - x) < 14 ? 0 : Math.Sign(target.X - x) * nest.CrawlSpeed * (float)delta;
            state.VerticalSpeed = Math.Max(-240, state.VerticalSpeed - 480 * (float)delta);
            float dy = state.VerticalSpeed * (float)delta;
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy))));
            for (int i = 0; i < steps; i++)
            {
                if (Free(world, state.X + dx / steps, state.Height)) state.X += dx / steps;
                if (Free(world, state.X, state.Height + dy / steps)) state.Height += dy / steps;
                else state.VerticalSpeed = 0;
            }
        }
        private static bool Free(ObjectSession world, float x, float h)
        {
            if (x < 8 || x > 5112 || h < PlayableTerrain.MinimumHeight) return false;
            var map = world.Terrain.Map;
            for (int i = -1; i <= 1; i++)
                if (TerrainHeroMotion.Solid(map, x + i * 5, h + 1) || TerrainHeroMotion.Solid(map, x + i * 5, h + 11)) return false;
            return true;
        }
    }
}
