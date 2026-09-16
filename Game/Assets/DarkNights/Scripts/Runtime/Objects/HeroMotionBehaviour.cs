using System;
using DarkNights.Core.Config;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 主角与释放后空中角色共用的权威纵向运动能力；所有持久量写回 ActorState。
    /// 固定步积分、下降扫过平台着地；S 仅短暂忽略当前支撑平台，地面永远不可下穿。
    /// </summary>
    public sealed partial class HeroMotionBehaviour : PooledBehaviour
    {
        [Inject] private ActorBehaviour actor;
        public bool Grounded => actor.Read().SupportPlatform >= 0;

        internal void Tick(double delta, bool jump, bool drop, bool thrust)
        {
            HeroControlDefinition rules = actor.World.Catalog.Balance.HeroControl;
            if (rules == null) return;
            ActorState state = actor.Edit();
            state.DropRemaining = Math.Max(0, state.DropRemaining - delta);
            if (state.SupportPlatform > 0)
            {
                PlatformDefinition support = null;
                foreach (var platform in actor.World.Layout.Platforms)
                    if (platform.Id == state.SupportPlatform) { support = platform; break; }
                if (support == null || !support.Contains(state.X)) state.SupportPlatform = -1;
            }
            if (drop && state.SupportPlatform > 0)
            {
                state.IgnoredPlatform = state.SupportPlatform;
                state.DropRemaining = rules.DropSeconds;
                state.SupportPlatform = -1;
                state.VerticalSpeed = -15;
            }
            else if (jump && Grounded)
            {
                state.VerticalSpeed = rules.JumpSpeed;
                state.SupportPlatform = -1;
            }
            if (Grounded)
            {
                state.VerticalSpeed = 0;
                state.JetpackFuel = Math.Min(rules.FuelSeconds, state.JetpackFuel + rules.FuelRecovery * delta);
                return;
            }
            float previous = state.Height;
            state.VerticalSpeed -= rules.Gravity * (float)delta;
            if (thrust && !jump && state.JetpackEquipped && state.JetpackFuel > 0)
            {
                float fraction = (float)Math.Min(1, state.JetpackFuel / delta);
                state.VerticalSpeed += (rules.Gravity + rules.JetpackSpeed * 4) * (float)delta * fraction;
                state.VerticalSpeed = Math.Min(state.VerticalSpeed, rules.JetpackSpeed);
                state.JetpackFuel = Math.Max(0, state.JetpackFuel - delta);
            }
            state.Height = Math.Min(rules.MaximumHeight, previous + state.VerticalSpeed * (float)delta);
            if (state.Height >= rules.MaximumHeight) state.VerticalSpeed = Math.Min(0, state.VerticalSpeed);
            float landing = 0;
            int supportId = 0;
            if (state.VerticalSpeed <= 0)
                foreach (var platform in actor.World.Layout.Platforms)
                    if (platform.Contains(state.X) && platform.Height <= previous + 0.001f &&
                        platform.Height >= state.Height && platform.Height > landing &&
                        !(state.DropRemaining > 0 && platform.Id == state.IgnoredPlatform))
                    { landing = platform.Height; supportId = platform.Id; }
            if (state.Height <= landing && state.VerticalSpeed <= 0)
            {
                state.Height = landing; state.VerticalSpeed = 0; state.SupportPlatform = supportId;
            }
        }
    }
}
