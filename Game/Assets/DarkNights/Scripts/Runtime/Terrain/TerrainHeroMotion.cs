using System;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>主角按权威逻辑格执行有界分步碰撞；位置和速度仍只写 ActorState，Sprite、Collider 与客户端不参与结算。</summary>
    public static class TerrainHeroMotion
    {
        private const float HalfWidth = 5, BodyHeight = 22;
        public static bool Solid(IReadOnlyGrid map, float x, float height)
        {
            int u = (int)Math.Floor(x / PlayableTerrain.CellPixels + .5);
            int y = (int)Math.Floor((PlayableTerrain.OriginY - height) / PlayableTerrain.CellPixels + .5);
            if (u < 0 || u >= TerrainGenerationSettings.Width || y >= TerrainGenerationSettings.Height) return true;
            if (y < 0) return false;
            var sample = map.Read(new CellCoord(u, -y));
            if (!sample.TryGetCell(out var cell)) return true;
            if (cell.IsEmpty) return false;
            return TerrainShapeGeometry.Contains(TerrainShapeGeometry.Decode(cell.Flags),
                x / PlayableTerrain.CellPixels - u + .5f, (height - PlayableTerrain.OriginY) / PlayableTerrain.CellPixels + y + .5f);
        }
        private static bool Blocked(IReadOnlyGrid map, float x, float height)
        {
            for (float y = 1; y <= BodyHeight; y += 7)
                if (Solid(map, x - HalfWidth, height + y) || Solid(map, x + HalfWidth, height + y)) return true;
            return false;
        }
        private static bool Supported(IReadOnlyGrid map, float x, float h) => Ground(map, x, h + .2f, h - .25f, out _);
        private static bool Ground(IReadOnlyGrid map, float x, float high, float low, out float height)
        {
            height = float.NegativeInfinity;
            for (int foot = -1; foot <= 1; foot++)
            {
                float px = x + foot * HalfWidth;
                int u = (int)Math.Floor(px / PlayableTerrain.CellPixels + .5f);
                int top = (int)Math.Floor((PlayableTerrain.OriginY - high) / PlayableTerrain.CellPixels + .5f);
                int bottom = (int)Math.Floor((PlayableTerrain.OriginY - low) / PlayableTerrain.CellPixels + .5f) + 1;
                for (int y = top; y <= bottom; y++)
                {
                    if (!map.Read(new CellCoord(u, -y)).TryGetCell(out var cell) || cell.IsEmpty) continue;
                    var shape = TerrainShapeGeometry.Decode(cell.Flags);
                    float edge = TerrainShapeGeometry.Ceiling(shape) ? 1 : TerrainShapeGeometry.Edge(shape, px / PlayableTerrain.CellPixels - u + .5f);
                    float surface = PlayableTerrain.OriginY + (-y - .5f + edge) * PlayableTerrain.CellPixels;
                    if (surface <= high + .001f && surface >= low - .001f) height = Math.Max(height, surface);
                }
            }
            return !float.IsNegativeInfinity(height);
        }
        public static void MoveHorizontal(IReadOnlyGrid map, ActorState state, float target)
        {
            float previous = state.X;
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(target - previous) / 2));
            for (int i = 1; i <= steps; i++)
            {
                float next = previous + (target - previous) * i / steps;
                float h = state.Height;
                if (state.VerticalSpeed <= 0 && Supported(map, state.X, h) &&
                    Ground(map, next, h + 2.05f, h - 2.05f, out float floor)) h = floor;
                if (Blocked(map, next, h)) break;
                state.X = next; state.Height = h;
            }
        }
        public static void Tick(IReadOnlyGrid map, ActorState state, HeroControlDefinition rules, double delta, bool jump, bool thrust)
        {
            bool grounded = state.VerticalSpeed <= 0 && Supported(map, state.X, state.Height);
            state.DropRemaining = 0; state.IgnoredPlatform = 0; state.SupportPlatform = grounded ? 0 : -1;
            if (grounded && jump) { state.VerticalSpeed = rules.JumpSpeed; grounded = false; state.SupportPlatform = -1; }
            if (grounded)
            {
                state.VerticalSpeed = 0;
                state.JetpackFuel = Math.Min(rules.FuelSeconds, state.JetpackFuel + rules.FuelRecovery * delta); return;
            }
            state.VerticalSpeed = Math.Max(-900, state.VerticalSpeed - rules.Gravity * (float)delta);
            if (thrust && !jump && state.JetpackEquipped && state.JetpackFuel > 0)
            {
                float fraction = (float)Math.Min(1, state.JetpackFuel / delta);
                state.VerticalSpeed = Math.Min(rules.JetpackSpeed, state.VerticalSpeed + (rules.Gravity + rules.JetpackSpeed * 4) * (float)delta * fraction);
                state.JetpackFuel = Math.Max(0, state.JetpackFuel - delta);
            }
            float from = state.Height, target = Math.Clamp(from + state.VerticalSpeed * (float)delta, PlayableTerrain.MinimumHeight, rules.MaximumHeight);
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(target - from) / 2));
            for (int i = 1; i <= steps; i++)
            {
                float next = from + (target - from) * i / steps;
                if (state.VerticalSpeed <= 0 && Ground(map, state.X, state.Height + .1f, next - .1f, out float floor))
                { state.Height = floor; state.VerticalSpeed = 0; state.SupportPlatform = 0; return; }
                if (Blocked(map, state.X, next))
                { state.VerticalSpeed = 0; return; }
                state.Height = next;
            }
            if (target == rules.MaximumHeight || target == PlayableTerrain.MinimumHeight) state.VerticalSpeed = 0;
        }
    }
}
