using System;
using DarkNights.Core.Logic.State;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 可装配的玩家决策能力，接管时撤销自动订单；输入、占用和背包均归所属 ActorState。
    /// 本类不读取设备或连接；服务端验证后才写入意图，与自动控制每步只能有一个推进者。
    /// </summary>
    public sealed partial class HeroControlBehaviour : PooledBehaviour
    {
        [Inject] private ActorBehaviour actor;
        [Inject] private HeroMotionBehaviour motion;
        [Inject] private ActorCombatBehaviour combat;

        internal bool Tick(double delta)
        {
            ActorState state = actor.Edit();
            bool manual = state.ManualControl;
            if (!manual && state.Height == 0 && state.SupportPlatform == 0) return false;
            bool jump = manual && state.JumpPending;
            bool drop = manual && state.DropPending;
            state.JumpPending = state.DropPending = false;
            if (manual && (state.Horizontal != 0 || jump || drop) &&
                (state.Activity == ActorActivity.Work || state.Activity == ActorActivity.Build)) actor.World.Work.Clear(actor);
            if (manual && state.Horizontal != 0)
            {
                state.X = Math.Clamp(state.X + state.Horizontal * (float)(actor.Definition.Speed * delta),
                    16, actor.World.Layout.WorldWidth - 16);
                state.Face = state.Horizontal;
                state.Walking = true;
            }
            motion.Tick(delta, jump, drop || (!manual && state.SupportPlatform > 0), manual && state.JumpHeld);
            if (!manual) return true;
            if (state.Height > 0 && (state.Activity == ActorActivity.Work || state.Activity == ActorActivity.Build))
                actor.World.Work.Clear(actor);
            if (!state.UseHeld && (state.Activity == ActorActivity.Work || state.Activity == ActorActivity.Build))
                actor.World.Work.Clear(actor);
            if (state.Activity == ActorActivity.Attack) combat.TickManualAttack(delta, state.UseHeld);
            return true;
        }

        internal void Claim(int slot, int generation)
        {
            actor.World.Work.Clear(actor);
            ActorState state = actor.Edit();
            state.ManualControl = true;
            state.ControllerSlot = slot; state.ControllerGeneration = generation;
            state.ControlLease = checked(state.ControlLease + 1);
            ResetInput(state);
        }

        internal void Release()
        {
            actor.World.Work.Clear(actor);
            ActorState state = actor.Edit();
            state.ManualControl = false;
            state.ControllerSlot = -1; state.ControllerGeneration = 0;
            state.ControlLease = checked(state.ControlLease + 1);
            ResetInput(state);
        }

        internal static void ResetInput(ActorState state)
        {
            state.Horizontal = 0;
            state.JumpHeld = state.UseHeld = state.JumpPending = state.DropPending = false;
            state.LastInputSequence = 0; state.LastInputTick = 0;
        }
    }
}
