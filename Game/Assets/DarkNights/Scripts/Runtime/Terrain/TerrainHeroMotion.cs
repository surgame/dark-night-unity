using System;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>
    /// 主角按权威逻辑格执行有界分步碰撞；身体占据和落地共用完整宽度的形状查询。
    /// 浅层脚底重叠只在下降或静止且净空有效时恢复；位置、速度和支撑仍只写 ActorState。
    /// </summary>
    public static class TerrainHeroMotion
    {
        private static float HalfWidth(ActorState state) => state.ManualControl ? HeroControlDefinition.BodyHalfWidth : 5;
        private static float BodyHeight(ActorState state) => state.ManualControl ? HeroControlDefinition.BodyHeight : 22;
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
        private static bool Blocked(IReadOnlyGrid map, ActorState state, float x, float height) =>
            TerrainBodyCollision.Blocked(map, x, height, HalfWidth(state), BodyHeight(state));
        private static bool Ground(IReadOnlyGrid map, ActorState state, float x, float high, float low, out float height) =>
            TerrainBodyCollision.Ground(map, x, high, low, HalfWidth(state), BodyHeight(state), out height);
        private static bool Supported(IReadOnlyGrid map, ActorState state, float x, float h, out float height) =>
            Ground(map, state, x, h + .2f, h - .25f, out height) ||
            // 恢复旧点采样允许的不足一像素脚底重叠；完整身体净空不成立时不能抬起角色。
            (Blocked(map, state, x, h) && Ground(map, state, x, h + 1.05f, h - .25f, out height));
        public static void MoveHorizontal(IReadOnlyGrid map, ActorState state, float target)
        {
            float previous = state.X;
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(target - previous) / 2));
            for (int i = 1; i <= steps; i++)
            {
                float next = previous + (target - previous) * i / steps;
                float h = state.Height;
                if (state.VerticalSpeed <= 0 && Supported(map, state, state.X, h, out float support))
                {
                    h = support;
                    if (Ground(map, state, next, h + 2.05f, h - 2.05f, out float floor)) h = floor;
                }
                if (Blocked(map, state, next, h)) break;
                state.X = next; state.Height = h;
            }
        }
        public static void Tick(IReadOnlyGrid map, ActorState state, HeroControlDefinition rules, double delta, bool jump, bool thrust,
            float? maximumHeight = null)
        {
            float ceiling = maximumHeight ?? rules.MaximumHeight;
            float support = state.Height;
            bool grounded = state.VerticalSpeed <= 0 && Supported(map, state, state.X, state.Height, out support);
            if (grounded) state.Height = support;
            state.DropRemaining = 0; state.IgnoredPlatform = 0; state.SupportPlatform = grounded ? 0 : -1;
            if (grounded && jump) { state.VerticalSpeed = rules.JumpSpeed; grounded = false; state.SupportPlatform = -1; }
            if (grounded)
            {
                state.VerticalSpeed = 0;
                if (state.JetpackOwned)
                    state.JetpackFuel = Math.Min(rules.FuelSeconds, state.JetpackFuel + rules.FuelRecovery * delta);
                return;
            }
            state.VerticalSpeed = Math.Max(-900, state.VerticalSpeed - rules.Gravity * (float)delta);
            if (thrust && !jump && state.JetpackOwned && state.JetpackEquipped && state.JetpackFuel > 0)
            {
                float fraction = (float)Math.Min(1, state.JetpackFuel / delta);
                state.VerticalSpeed = Math.Min(rules.JetpackSpeed, state.VerticalSpeed + (rules.Gravity + rules.JetpackSpeed * 4) * (float)delta * fraction);
                state.JetpackFuel = Math.Max(0, state.JetpackFuel - delta);
            }
            float from = state.Height, target = Math.Clamp(from + state.VerticalSpeed * (float)delta, PlayableTerrain.MinimumHeight, ceiling);
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(target - from) / 2));
            for (int i = 1; i <= steps; i++)
            {
                float next = from + (target - from) * i / steps;
                if (state.VerticalSpeed <= 0 && Ground(map, state, state.X, state.Height + .1f, next - .1f, out float floor))
                { state.Height = floor; state.VerticalSpeed = 0; state.SupportPlatform = 0; return; }
                if (Blocked(map, state, state.X, next))
                { state.VerticalSpeed = 0; return; }
                state.Height = next;
            }
            if (target == ceiling || target == PlayableTerrain.MinimumHeight) state.VerticalSpeed = 0;
        }
    }
}
