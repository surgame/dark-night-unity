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
            operation == SessionOperation.UseHeroItem || operation == SessionOperation.Expedition ||
            operation == SessionOperation.SelectDestination || operation == SessionOperation.CancelJourney ||
            operation == SessionOperation.SellCarriedOre || operation == SessionOperation.BuyEquipment;

        internal int AssignDefault(SessionConnection connection)
        {
            if (connection == null || world.Camp.Read().Mode != SessionMode.Playing ||
                world.Catalog.Balance.HeroControl == null) return 0;
            ActorBehaviour current = FindControlledBy(connection.PlayerSlot);
            if (current != null)
            {
                connection.DefaultHeroId = current.Id;
                connection.DefaultHeroRecoveryPending = false;
                return current.Read().ControllerGeneration == connection.Generation ? 0 : Claim(connection, current);
            }
            ActorBehaviour candidate = FindDefaultCandidate(connection);
            return candidate != null ? Claim(connection, candidate) : SpawnDefault(connection);
        }

        private ActorBehaviour FindControlledBy(int slot)
        {
            foreach (var actor in world.Index.Actors)
                if (actor.Read().ControllerSlot == slot) return actor;
            return null;
        }

        private ActorBehaviour FindDefaultCandidate(SessionConnection connection)
        {
            if (world.IsExpedition)
                foreach (var saved in world.Index.Actors)
                    if (saved.Read().OwnerSlot == connection.PlayerSlot && CanClaim(saved)) return saved;
            ActorBehaviour actor = world.Index.Find<ActorBehaviour>(connection.DefaultHeroId);
            if (connection.DefaultHeroRecoveryPending && actor?.Read().ManualControl != true) actor = null;
            if (CanClaim(actor)) return actor;
            foreach (var restored in world.Index.Actors)
            {
                if (!restored.Read().ManualControl || !CanClaim(restored) ||
                    world.IsExpedition && restored.Read().OwnerSlot >= 0 && restored.Read().OwnerSlot != connection.PlayerSlot) continue;
                return restored;
            }
            return null;
        }

        private int SpawnDefault(SessionConnection connection)
        {
            ActorBehaviour actor;
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
            switch (request.Operation)
            {
                case SessionOperation.SelectDestination:
                case SessionOperation.CancelJourney:
                    return SessionJourneyControl.Apply(world, connection, request);
                case SessionOperation.Expedition:
                    return SessionExpeditionControl.Apply(world, connection, request);
                case SessionOperation.SellCarriedOre:
                case SessionOperation.BuyEquipment:
                    return ApplyTrade(connection, request);
                case SessionOperation.ClaimHero:
                    return ApplyClaim(connection, request.ActorIds[0]);
                case SessionOperation.ReleaseHero:
                case SessionOperation.SelectHeroItem:
                case SessionOperation.UseHeroItem:
                    return ApplyOwned(connection, request);
                default:
                    return 0;
            }
        }

        private int ApplyClaim(SessionConnection connection, int actorId)
        {
            ActorBehaviour actor = world.Index.Find<ActorBehaviour>(actorId);
            if (!CanClaim(actor) || FindControlledBy(connection.PlayerSlot) != null) return 0;
            return Claim(connection, actor) > 0 ? 1 : 0;
        }

        private int ApplyTrade(SessionConnection connection, SessionRequest request)
        {
            ActorBehaviour actor = world.Index.Find<ActorBehaviour>(request.ActorIds[0]);
            if (!Owns(actor, connection, request.ControlLease) || world.Camp.Read().Mode != SessionMode.Playing)
                return 0;
            return world.Mutations.Run(() => request.Operation == SessionOperation.SellCarriedOre
                ? world.Trade.Sell(actor, request.TargetId, request.Value, (int)request.X)
                : world.Trade.Buy(actor, request.TargetId, request.Kind, request.Value));
        }

        private int ApplyOwned(SessionConnection connection, SessionRequest request)
        {
            ActorBehaviour actor = world.Index.Find<ActorBehaviour>(request.ActorIds[0]);
            if (actor == null || actor.Enemy || actor.Hp <= 0 || actor.IsTraining ||
                world.Camp.Read().Mode != SessionMode.Playing || world.Catalog.Balance.HeroControl == null) return 0;
            var control = actor.Object.GetBehaviour<HeroControlBehaviour>();
            if (control == null || !Owns(actor, connection, request.ControlLease)) return 0;
            int affected = world.Mutations.Run(() =>
            {
                if (request.Operation == SessionOperation.ReleaseHero)
                {
                    control.Release();
                    return 1;
                }
                if (world.Paused || world.IsExpedition && actor.Read().Boarded) return 0;
                var inventory = actor.Object.GetBehaviour<HeroInventoryBehaviour>();
                if (inventory == null) return 0;
                switch (request.Operation)
                {
                    case SessionOperation.SelectHeroItem:
                        return inventory.Select(request.Value) ? 1 : 0;
                    case SessionOperation.UseHeroItem:
                        return inventory.Use(request.Kind, request.Value, request.TargetId) ? 1 : 0;
                    default:
                        return 0;
                }
            });
            if (affected > 0 && request.Operation == SessionOperation.ReleaseHero) connection.DefaultHeroId = 0;
            return affected;
        }

        internal bool Receive(SessionConnection connection, HeroInputRequest input, long tick)
        {
            if (!ValidInput(input, tick)) return false;
            var actor = world.Index.Find<ActorBehaviour>(input.ActorId);
            if (!Owns(actor, connection, input.ControlLease) || input.Sequence <= actor.Read().LastInputSequence) return false;
            return world.Mutations.Run(() =>
            {
                ApplyInput(actor.Edit(), actor.Id, input, tick);
                return true;
            });
        }

        private bool ValidInput(HeroInputRequest input, long tick)
        {
            if (input.ActorId <= 0 || input.ControlLease <= 0 || input.Sequence <= 0) return false;
            if (input.Horizontal < -1 || input.Horizontal > 1 || input.ObservedTick < 0 ||
                input.ObservedTick > tick || tick - input.ObservedTick > 60) return false;
            if (world.Paused || world.Camp.Read().Mode != SessionMode.Playing) return false;
            if (input.Mining.Present)
            {
                var map = world.Terrain?.Map;
                if (input.Mining.Kind != DarkNights.Core.ViewData.HeroMiningTargetKind.Foreground &&
                    input.Mining.Kind != DarkNights.Core.ViewData.HeroMiningTargetKind.MineralDeposit ||
                    input.Mining.Kind == DarkNights.Core.ViewData.HeroMiningTargetKind.MineralDeposit &&
                        input.Mining.EntityId != 0 ||
                    input.Mining.Kind == DarkNights.Core.ViewData.HeroMiningTargetKind.Foreground &&
                        (input.Mining.EntityId != 0 || input.Mining.MineralContentVersion != 0))
                    return false;
                if (map == null || input.Mining.WorldId.Length != 32 ||
                    input.Mining.WorldId != map.World.WorldId.ToString().Replace("-", "") ||
                    input.Mining.MapEpoch != map.World.Epoch ||
                    !map.Descriptor.Bounds.Contains(new AnyRules.Next.CellCoord(input.Mining.U, input.Mining.V))) return false;
            }
            return !float.IsNaN(input.AimAngle) && !float.IsInfinity(input.AimAngle) &&
                Math.Abs(input.AimAngle) <= 180 && input.SelectionRevision >= 0;
        }

        private void ApplyInput(ActorState state, int actorId, HeroInputRequest input, long tick)
        {
            state.LastInputSequence = input.Sequence; state.LastInputTick = tick;
            state.Horizontal = input.Horizontal; state.JumpHeld = input.JumpHeld; state.SprintHeld = input.SprintHeld;
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
                }
            }
            state.JumpPending |= input.JumpPressed;
#if UNITY_EDITOR
            Terrain.TerrainJumpTrace.Received(world.Terrain?.Map, state, input, tick);
#endif
            bool pilot = world.IsExpedition && world.Expedition.Ship?.Read().PilotId == actorId;
            state.DropPending = pilot ? input.DropPressed : state.DropPending || input.DropPressed;
        }

        internal void Expire(long tick)
        {
            foreach (var actor in world.Index.Actors)
            {
                var current = actor.Read();
                if (!current.ManualControl || (!world.Paused && tick - current.LastInputTick <= InputTimeoutTicks) ||
                    (current.Horizontal == 0 && !current.SprintHeld && !current.JumpHeld && !current.UseHeld && !current.Charging && !current.UsePressed && !current.UseReleased && !current.JumpPending && !current.DropPending && !current.PickaxeSwingActive && string.IsNullOrEmpty(current.MiningWorldId))) continue;
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
            state.Horizontal = 0; state.SprintHeld = false; state.JumpHeld = state.UseHeld = state.JumpPending = state.DropPending = false;
            HeroJumpMotion.Clear(state);
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
        private bool CanClaim(ActorBehaviour actor) => actor != null && !actor.Enemy && (actor.Hp > 0 || world.IsExpedition && actor.Read().OwnerSlot >= 0) && !actor.IsTraining &&
            actor.Read().ControllerSlot < 0 && actor.Object.GetBehaviour<HeroControlBehaviour>() != null &&
            world.Camp.Read().Mode == SessionMode.Playing && world.Catalog.Balance.HeroControl != null;
        private static bool Owns(ActorBehaviour actor, SessionConnection connection, int lease) =>
            actor != null && connection != null && actor.Hp > 0 && !actor.Enemy && actor.Read().ManualControl &&
            actor.Read().ControllerSlot == connection.PlayerSlot && actor.Read().ControllerGeneration == connection.Generation &&
            lease > 0 && actor.Read().ControlLease == lease;
    }
}
