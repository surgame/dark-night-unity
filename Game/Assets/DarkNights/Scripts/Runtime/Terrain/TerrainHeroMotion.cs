using System;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>
    /// 统一移动能力的权威地形后端；身体占据和落地共用完整宽度的形状查询。
    /// 浅层脚底重叠只在下降或静止且净空有效时恢复；位置、速度和支撑仍只写 ActorState。
    /// 默认仅基础跳跃；历史喷气仅供离线工作台显式启用，不由保存的持有标记自动恢复。
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
            float? maximumHeight = null, float? targetX = null, HeroJumpStrategy? jumpStrategy = null, bool inputPrepared = false,
            bool jetpack = false, MovementDefinition movement = null, float speedMultiplier = 1, float? maximumX = null,
            float? direction = null, float? normalSpeed = null, float? goalX = null)
        {
            if (!inputPrepared) HeroJumpMotion.Sample(state, jump && (movement?.CanJump ?? true), delta);
            if (movement?.CanJump == false) state.JumpBufferRemaining = 0;
#if UNITY_EDITOR
            var trace = TerrainMotionTrace.Begin(map, state, delta, jump, thrust, targetX);
#endif
            float ceiling = maximumHeight ?? rules.MaximumHeight;
            float support = state.Height;
            bool grounded = state.VerticalSpeed <= 0 && Supported(map, state, state.X, state.Height, out support);
#if UNITY_EDITOR
            trace.GroundedAtStart = grounded;
#endif
            if (grounded) state.Height = support;
            if (movement != null && !targetX.HasValue)
                targetX = MovementHorizontalMotion.Target(state, rules, movement, grounded, delta,
                    maximumX ?? TerrainGenerationSettings.Width * PlayableTerrain.CellPixels - 16,
                    speedMultiplier, direction, normalSpeed, goalX);
            state.DropRemaining = 0; state.IgnoredPlatform = 0; state.SupportPlatform = grounded ? 0 : -1;
            bool started = (movement?.CanJump ?? true) && HeroJumpMotion.TryStart(state, rules, grounded);
            if (started) grounded = false;
            if (grounded) state.VerticalSpeed = 0;
            else HeroJumpMotion.Accelerate(state, rules, delta, thrust, jumpStrategy, jetpack);
            float from = state.Height, target = Math.Clamp(from + state.VerticalSpeed * (float)delta, PlayableTerrain.MinimumHeight, ceiling);
            float distance = (targetX ?? state.X) - state.X;
            if (started) distance = TerrainBodySweep.LaunchDistance(map, state, distance, target - from);
            float previousX = state.X;
            grounded = TerrainBodySweep.Move(map, state, distance, target - from, grounded);
            if (grounded) HeroJumpMotion.Land(state, rules, delta, recoverFuel: jetpack);
            else state.SupportPlatform = -1;
            if (movement != null)
            {
                if (grounded) MovementHorizontalMotion.Land(state, rules, speedMultiplier, direction, normalSpeed);
                if (Math.Abs(state.X - previousX - distance) > .01f) state.HorizontalSpeed = 0;
            }
            if (state.Height >= ceiling || state.Height <= PlayableTerrain.MinimumHeight)
            { state.VerticalSpeed = 0; state.JumpAscending = false; }
#if UNITY_EDITOR
            TerrainMotionTrace.End(map, state, trace, started);
#endif
        }
    }
}
