using System;
using DarkNights.Core.Config;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 普通跳跃、落地前输入保留与喷气衔接的共用规则；临时量只写所属 ActorState。
    /// 一次按下只消费一次，不提供空中重跳；固定策略保留原曲线，可变策略只缩短早松键的上升。
    /// </summary>
    public static class HeroJumpMotion
    {
        public const double BufferSeconds = .1;
        public const float ReleaseSpeedRatio = .45f;

        public static void Sample(ActorState state, bool pressed, double delta)
        {
            state.JumpBufferRemaining = pressed ? BufferSeconds : Math.Max(0, state.JumpBufferRemaining - delta);
        }

        public static bool TryStart(ActorState state, HeroControlDefinition rules, bool grounded)
        {
            if (!grounded || state.JumpBufferRemaining <= 0) return false;
            state.JumpBufferRemaining = 0;
            state.JumpAscending = true;
            state.VerticalSpeed = rules.JumpSpeed;
            state.SupportPlatform = -1;
            return true;
        }

        public static void Accelerate(ActorState state, HeroControlDefinition rules, double delta, bool held,
            HeroJumpStrategy? strategy = null, bool jetpack = true)
        {
            if ((strategy ?? rules.JumpStrategy) == HeroJumpStrategy.HoldHeight && state.JumpAscending && !held)
                state.VerticalSpeed = Math.Min(state.VerticalSpeed, rules.JumpSpeed * ReleaseSpeedRatio);
            state.VerticalSpeed = Math.Max(-900, state.VerticalSpeed - rules.Gravity * (float)delta);
            if (state.VerticalSpeed <= 0) state.JumpAscending = false;
            // 普通跳跃上升期间保留完整曲线；喷气只在该阶段结束后衔接，不把已有高速强行压低。
            if (!jetpack || !held || state.JumpAscending || !state.JetpackOwned || !state.JetpackEquipped || state.JetpackFuel <= 0)
                return;
            float fraction = (float)Math.Min(1, state.JetpackFuel / delta);
            if (state.VerticalSpeed < rules.JetpackSpeed)
                state.VerticalSpeed = Math.Min(rules.JetpackSpeed,
                    state.VerticalSpeed + (rules.Gravity + rules.JetpackSpeed * 4) * (float)delta * fraction);
            state.JetpackFuel = Math.Max(0, state.JetpackFuel - delta);
        }

        public static void Land(ActorState state, HeroControlDefinition rules, double delta, int support = 0)
        {
            state.VerticalSpeed = 0; state.SupportPlatform = support; state.JumpAscending = false;
            if (state.JetpackOwned)
                state.JetpackFuel = Math.Min(rules.FuelSeconds, state.JetpackFuel + rules.FuelRecovery * delta);
        }

        public static void Clear(ActorState state)
        {
            state.JumpBufferRemaining = 0; state.JumpAscending = false;
        }
    }
}
