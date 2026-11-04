using DarkNights.Runtime.Session;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 单个角色的玩家控制能力，应用会话已验证的操作并管理接管、释放与输入清理。
    /// 输入、控制者和动作只写所属ActorState；超时清理保留控制权和序号，重新接管重置输入身份。
    /// </summary>
    public sealed partial class PlayerControlBehaviour : PooledBehaviour
    {
        [Inject] private ActorBehaviour actor;
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
            ClearHeldInput(state);
            state.ShipEntryBlocked = false;
            state.LastInputSequence = 0; state.LastInputTick = 0;
        }

        internal void ClearInput() => ClearHeldInput(actor.Edit());

        internal void InvalidateInput()
        {
            ActorState state = actor.Edit();
            ClearHeldInput(state);
            state.ControlLease = checked(state.ControlLease + 1);
        }

        internal void ApplyInput(HeroInputRequest input, long tick)
        {
            ActorState state = actor.Edit();
            ObjectSession world = actor.World;
            state.LastInputSequence = input.Sequence; state.LastInputTick = tick;
            state.LightAimAngle = input.LightAimAngle;
            state.Horizontal = input.Horizontal; state.Climb = input.Climb; state.JumpHeld = input.JumpHeld; state.SprintHeld = input.SprintHeld;
            if (input.CancelUse || input.SelectionRevision != state.SelectionRevision)
            { state.AimAngle = input.AimAngle; HeroEquipment.Cancel(state); }
            else
            {
                bool preservePressedTarget = state.UsePressed && world.Resources.Equipment.Mining(HeroInventoryBehaviour.Slot(state, state.SelectedItem)) != null;
                state.UseHeld = input.UseHeld;
                state.UsePressed |= input.UsePressed;
                state.UseReleased |= input.UseReleased;
                if (!preservePressedTarget)
                {
                    state.AimAngle = input.AimAngle;
                    state.MiningWorldId = input.Mining.WorldId;
                    state.MiningMapEpoch = input.Mining.MapEpoch;
                    state.MiningU = input.Mining.U; state.MiningV = input.Mining.V;
                    state.MiningTileId = input.Mining.TileId; state.MiningFlags = input.Mining.Flags;
                    state.MiningTargetKind = input.Mining.Kind; state.MiningEntityId = input.Mining.EntityId;
                    state.MiningContentVersion = input.Mining.ContentVersion;
                    state.MiningMineralContentVersion = input.Mining.MineralContentVersion;
                    if (input.Mining.Present && world.Geology.ReservesSource(new AnyRules.Next.CellCoord(input.Mining.U, input.Mining.V)))
                    {
                        state.MiningWorldId = null;
                        state.MiningTargetKind = DarkNights.Core.ViewData.HeroMiningTargetKind.None;
                    }
                }
            }
            state.JumpPending |= input.JumpPressed;
#if UNITY_EDITOR
            Terrain.TerrainJumpTrace.Received(world.Terrain?.Map, state, input, tick);
#endif
            bool pilot = world.IsExpedition && world.Expedition.Ship?.Read().PilotId == actor.Id;
            state.DropPending = pilot ? input.DropPressed : state.DropPending || input.DropPressed;
        }

        private static void ClearHeldInput(ActorState state)
        {
            HeroEquipment.Cancel(state);
            state.Horizontal = state.Climb = 0; state.SprintHeld = false;
            state.JumpHeld = state.UseHeld = state.JumpPending = state.DropPending = false;
            HeroJumpMotion.Clear(state, clearVelocity: false);
        }
    }
}
