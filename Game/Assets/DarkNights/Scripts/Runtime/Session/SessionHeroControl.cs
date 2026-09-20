using System;
using DarkNights.Core.Logic.State;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 可信会话连接到角色能力的授权适配，不拥有角色、背包或第二份输入状态。
    /// 控制租约防止释放再接管后的迟到输入；500 ms 没有新输入归零，暂停仍执行超时检查。
    /// </summary>
    public sealed class SessionHeroControl
    {
        public const int InputTimeoutTicks = 30;
        private readonly ObjectSession world;
        internal SessionHeroControl(ObjectSession world) { this.world = world; }
        internal static bool IsOperation(SessionOperation operation) => operation == SessionOperation.ClaimHero ||
            operation == SessionOperation.ReleaseHero || operation == SessionOperation.SelectHeroItem ||
            operation == SessionOperation.UseHeroItem;

        internal int AssignDefault(SessionConnection connection)
        {
            if (connection == null || world.Camp.Read().Mode != SessionMode.Playing ||
                world.Catalog.Balance.HeroControl == null) return 0;
            foreach (var current in world.Index.Actors)
                if (current.Read().ControllerSlot == connection.PlayerSlot)
                {
                    connection.DefaultHeroId = current.Id;
                    connection.DefaultHeroRecoveryPending = false;
                    if (current.Read().ControllerGeneration == connection.Generation) return 0;
                    return Claim(connection, current);
                }
            ActorBehaviour actor = world.Index.Find<ActorBehaviour>(connection.DefaultHeroId);
            if (connection.DefaultHeroRecoveryPending && actor?.Read().ManualControl != true) actor = null;
            if (CanClaim(actor)) return Claim(connection, actor);
            foreach (var restored in world.Index.Actors)
            {
                if (!restored.Read().ManualControl || !CanClaim(restored)) continue;
                return Claim(connection, restored);
            }
            try
            {
                actor = world.Mutations.Run(() =>
                {
                    ActorBehaviour created = world.Commands.SpawnDefaultResident();
                    created?.Object.GetBehaviour<HeroControlBehaviour>().Claim(connection.PlayerSlot, connection.Generation);
                    return created;
                });
            }
            catch (InvalidOperationException) { return 0; }
            if (actor == null) return 0;
            connection.DefaultHeroId = actor.Id;
            connection.DefaultHeroRecoveryPending = false;
            return actor.Id;
        }

        internal int Apply(SessionConnection connection, SessionRequest request)
        {
            ActorBehaviour actor = world.Index.Find<ActorBehaviour>(request.ActorIds[0]);
            if (request.Operation == SessionOperation.ClaimHero)
            {
                if (!CanClaim(actor)) return 0;
                foreach (var current in world.Index.Actors)
                    if (current.Read().ControllerSlot == connection.PlayerSlot) return 0;
                return Claim(connection, actor) > 0 ? 1 : 0;
            }
            if (actor == null || actor.Enemy || actor.Hp <= 0 || actor.IsTraining ||
                world.Camp.Read().Mode != SessionMode.Playing || world.Catalog.Balance.HeroControl == null) return 0;
            var control = actor.Object.GetBehaviour<HeroControlBehaviour>();
            if (control == null) return 0;
            if (!Owns(actor, connection, request.ControlLease)) return 0;
            int affected = world.Mutations.Run(() =>
            {
                if (request.Operation == SessionOperation.ReleaseHero) { control.Release(); return 1; }
                if (world.Paused) return 0;
                var inventory = actor.Object.GetBehaviour<HeroInventoryBehaviour>();
                if (inventory == null) return 0;
                return (request.Operation == SessionOperation.SelectHeroItem ? inventory.Select(request.Value) :
                    inventory.Use(request.Kind, request.Value, request.TargetId)) ? 1 : 0;
            });
            if (affected > 0 && request.Operation == SessionOperation.ReleaseHero) connection.DefaultHeroId = 0;
            return affected;
        }

        internal bool Receive(SessionConnection connection, HeroInputRequest input, long tick)
        {
            if (input.ActorId <= 0 || input.ControlLease <= 0 || input.Sequence <= 0 ||
                input.Horizontal < -1 || input.Horizontal > 1 || input.ObservedTick < 0 || input.ObservedTick > tick ||
                tick - input.ObservedTick > 60 || world.Paused || world.Camp.Read().Mode != SessionMode.Playing) return false;
            if (float.IsNaN(input.AimAngle) || float.IsInfinity(input.AimAngle) || Math.Abs(input.AimAngle) > 180 || input.SelectionRevision < 0) return false;
            var actor = world.Index.Find<ActorBehaviour>(input.ActorId);
            if (!Owns(actor, connection, input.ControlLease) || input.Sequence <= actor.Read().LastInputSequence) return false;
            return world.Mutations.Run(() =>
            {
                ActorState state = actor.Edit();
                state.LastInputSequence = input.Sequence; state.LastInputTick = tick;
                state.Horizontal = input.Horizontal; state.JumpHeld = input.JumpHeld;
                state.AimAngle = input.AimAngle;
                if (input.CancelUse || input.SelectionRevision != state.SelectionRevision) HeroEquipment.Cancel(state);
                else
                {
                    state.UseHeld = input.UseHeld;
                    state.UsePressed |= input.UsePressed;
                    state.UseReleased |= input.UseReleased;
                }
                state.JumpPending |= input.JumpPressed; state.DropPending |= input.DropPressed;
                return true;
            });
        }

        internal void Expire(long tick)
        {
            foreach (var actor in world.Index.Actors)
            {
                var current = actor.Read();
                if (!current.ManualControl || (!world.Paused && tick - current.LastInputTick <= InputTimeoutTicks) ||
                    (current.Horizontal == 0 && !current.JumpHeld && !current.UseHeld && !current.Charging && !current.UsePressed && !current.UseReleased && !current.JumpPending && !current.DropPending)) continue;
                world.Mutations.Run(() => { ClearInput(actor.Edit()); return true; });
            }
        }

        internal void Release(int slot)
        {
            foreach (var actor in world.Index.Actors)
                if (actor.Read().ControllerSlot == slot)
                    world.Mutations.Run(() => { actor.Object.GetBehaviour<HeroControlBehaviour>().Release(); return true; });
        }

        internal void InvalidateInputs()
        {
            foreach (var actor in world.Index.Actors)
                if (actor.Read().ControllerSlot >= 0)
                    world.Mutations.Run(() =>
                    {
                        var state = actor.Edit();
                        ClearInput(state); state.ControlLease = checked(state.ControlLease + 1);
                        return true;
                    });
        }

        private static void ClearInput(ActorState state)
        {
            HeroEquipment.Cancel(state);
            state.Horizontal = 0; state.JumpHeld = state.UseHeld = state.JumpPending = state.DropPending = false;
        }
        private int Claim(SessionConnection connection, ActorBehaviour actor)
        {
            int id = world.Mutations.Run(() =>
            {
                actor.Object.GetBehaviour<HeroControlBehaviour>().Claim(connection.PlayerSlot, connection.Generation);
                return actor.Id;
            });
            connection.DefaultHeroId = id;
            connection.DefaultHeroRecoveryPending = false;
            return id;
        }
        private bool CanClaim(ActorBehaviour actor) => actor != null && !actor.Enemy && actor.Hp > 0 && !actor.IsTraining &&
            actor.Read().ControllerSlot < 0 && actor.Object.GetBehaviour<HeroControlBehaviour>() != null &&
            world.Camp.Read().Mode == SessionMode.Playing && world.Catalog.Balance.HeroControl != null;
        private static bool Owns(ActorBehaviour actor, SessionConnection connection, int lease) =>
            actor != null && connection != null && actor.Hp > 0 && !actor.Enemy && actor.Read().ManualControl &&
            actor.Read().ControllerSlot == connection.PlayerSlot && actor.Read().ControllerGeneration == connection.Generation &&
            lease > 0 && actor.Read().ControlLease == lease;
    }
}
