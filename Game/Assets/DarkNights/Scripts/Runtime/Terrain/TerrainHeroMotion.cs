using System;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
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
            return !sample.TryGetCell(out var cell) || !cell.IsEmpty;
        }
        private static bool Blocked(IReadOnlyGrid map, float x, float height)
        {
            for (float y = 1; y <= BodyHeight; y += 7)
                if (Solid(map, x - HalfWidth, height + y) || Solid(map, x + HalfWidth, height + y)) return true;
            return false;
        }
        private static bool Supported(IReadOnlyGrid map, float x, float h) =>
            Solid(map, x - HalfWidth, h - .1f) || Solid(map, x + HalfWidth, h - .1f);
        public static float MoveX(IReadOnlyGrid map, float previous, float target, float height)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(target - previous) / 2));
            float x = previous;
            for (int i = 1; i <= steps; i++)
            {
                float next = previous + (target - previous) * i / steps;
                if (Blocked(map, next, height)) break;
                x = next;
            }
            return x;
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
                if (Blocked(map, state.X, next) || (state.VerticalSpeed < 0 && Supported(map, state.X, next)))
                {
                    if (state.VerticalSpeed < 0)
                    {
                        // 精确吸附到被扫过格子的顶边，消除落地后的小间隙和下一帧抖动。
                        float row = (float)Math.Floor((PlayableTerrain.OriginY - next + .1f) / PlayableTerrain.CellPixels + .5f);
                        state.Height = PlayableTerrain.OriginY - (row - .5f) * PlayableTerrain.CellPixels;
                        state.SupportPlatform = 0;
                    }
                    state.VerticalSpeed = 0; return;
                }
                state.Height = next;
            }
            if (target == rules.MaximumHeight || target == PlayableTerrain.MinimumHeight) state.VerticalSpeed = 0;
        }
    }
}
