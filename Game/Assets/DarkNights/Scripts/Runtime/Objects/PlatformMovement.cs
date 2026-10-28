using System;
using DarkNights.Core.Config;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 无原生地形的旧平台环境计算，作为MovementBehaviour的普通函数后端，不独立装配或调度。
    /// 与地形、船舱共用同一水平速度算法；只读布局并写回角色唯一ActorState。
    /// </summary>
    internal static class PlatformMovement
    {
        internal static void Tick(ActorBehaviour actor, MovementDefinition movement, double delta,
            bool jump, bool drop, bool held, float direction, float? normalSpeed, float? goalX, float? appliedX)
        {
            var world = actor.World;
            var state = actor.Edit();
            HeroControlDefinition rules = world.Catalog.Balance.HeroControl;
            state.DropRemaining = Math.Max(0, state.DropRemaining - delta);
            if (state.SupportPlatform > 0)
            {
                PlatformDefinition support = null;
                foreach (var platform in world.Layout.Platforms)
                    if (platform.Id == state.SupportPlatform) { support = platform; break; }
                if (support == null || !support.Contains(state.X)) state.SupportPlatform = -1;
            }
            state.X = appliedX ?? MovementHorizontalMotion.Target(state, rules, movement,
                state.SupportPlatform >= 0, delta, world.Layout.WorldWidth - 16,
                world.DebugHeroSpeedMultiplier, direction, normalSpeed, goalX);
            if (state.SupportPlatform > 0)
                foreach (var platform in world.Layout.Platforms)
                    if (platform.Id == state.SupportPlatform && !platform.Contains(state.X))
                    { state.SupportPlatform = -1; break; }
            if (drop && state.SupportPlatform > 0)
            {
                state.IgnoredPlatform = state.SupportPlatform;
                state.DropRemaining = rules.DropSeconds;
                state.SupportPlatform = -1;
                state.VerticalSpeed = -15;
            }
            else if (movement.CanJump) HeroJumpMotion.TryStart(state, rules, state.SupportPlatform >= 0);
            if (state.SupportPlatform >= 0)
            {
                Land(actor, rules, delta, state.SupportPlatform, direction, normalSpeed);
                return;
            }
            float previous = state.Height;
            HeroJumpMotion.Accelerate(state, rules, delta, held, jetpack: false);
            state.Height = Math.Min(rules.MaximumHeight, previous + state.VerticalSpeed * (float)delta);
            if (state.Height >= rules.MaximumHeight) state.VerticalSpeed = Math.Min(0, state.VerticalSpeed);
            float landing = 0;
            int supportId = 0;
            if (state.VerticalSpeed <= 0)
                foreach (var platform in world.Layout.Platforms)
                    if (platform.Contains(state.X) && platform.Height <= previous + .001f &&
                        platform.Height >= state.Height && platform.Height > landing &&
                        !(state.DropRemaining > 0 && platform.Id == state.IgnoredPlatform))
                    { landing = platform.Height; supportId = platform.Id; }
            if (state.Height <= landing && state.VerticalSpeed <= 0)
            {
                state.Height = landing;
                Land(actor, rules, delta, supportId, direction, normalSpeed);
            }
        }

        private static void Land(ActorBehaviour actor, HeroControlDefinition rules, double delta,
            int support, float direction, float? normalSpeed)
        {
            HeroJumpMotion.Land(actor.Edit(), rules, delta, support, recoverFuel: false);
            MovementHorizontalMotion.Land(actor.Edit(), rules, actor.World.DebugHeroSpeedMultiplier, direction, normalSpeed);
        }
    }
}
