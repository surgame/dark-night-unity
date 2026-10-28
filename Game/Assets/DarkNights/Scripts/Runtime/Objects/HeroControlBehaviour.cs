using System;
using DarkNights.Core.Logic.State;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 可装配的玩家决策能力，接管时撤销自动订单；输入、占用和背包均归所属 ActorState。
    /// 本类不读取设备或连接、不执行坐标积分；ActorBehaviour在决策后调用唯一移动能力，再推进装备。
    /// </summary>
    public sealed partial class HeroControlBehaviour : PooledBehaviour
    {
        [Inject] private ActorBehaviour actor;
        [Inject] private MovementBehaviour movement;

        internal bool PrepareInput()
        {
            ActorState state = actor.Edit();
            if (!state.ManualControl) return false;
            if (state.Horizontal != 0) state.Face = state.Horizontal;
            if ((state.Horizontal != 0 || state.JumpPending || state.DropPending) &&
                (state.Activity == ActorActivity.Work || state.Activity == ActorActivity.Build)) actor.World.Work.Clear(actor);
            return true;
        }

        internal void AfterMovement(double delta)
        {
            if (movement.HandledByCabin) return;
            ActorState state = actor.Edit();
            HeroEquipment.Tick(actor, delta);
            if (state.Height > 0 && (state.Activity == ActorActivity.Work || state.Activity == ActorActivity.Build))
                actor.World.Work.Clear(actor);
            if (!state.UseHeld && (state.Activity == ActorActivity.Work || state.Activity == ActorActivity.Build))
                actor.World.Work.Clear(actor);
        }

        internal void Claim(int slot, int generation)
        {
            actor.World.Work.Clear(actor);
            ActorState state = actor.Edit();
            state.ManualControl = true;
            if (actor.World.IsExpedition) state.OwnerSlot = slot;
            state.ControllerSlot = slot; state.ControllerGeneration = generation;
            state.ControlLease = checked(state.ControlLease + 1);
            ResetInput(state);
        }

        internal void Release()
        {
            actor.World.Ship.ReleasePilot(actor.Id);
            actor.World.Work.Clear(actor);
            ActorState state = actor.Edit();
            state.ManualControl = actor.World.IsExpedition;
            state.ControllerSlot = -1; state.ControllerGeneration = 0;
            state.ControlLease = checked(state.ControlLease + 1);
            ResetInput(state);
            if (!state.ManualControl) MovementHorizontalMotion.Clear(state);
        }

        internal static void ResetInput(ActorState state)
        {
            HeroEquipment.Cancel(state);
            state.Horizontal = 0; state.SprintHeld = false; state.ShipEntryBlocked = false;
            state.JumpHeld = state.UseHeld = state.JumpPending = state.DropPending = false;
            HeroJumpMotion.Clear(state, clearVelocity: false);
            state.LastInputSequence = 0; state.LastInputTick = 0;
        }
    }
}
