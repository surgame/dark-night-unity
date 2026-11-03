using System;
using DarkNights.Core.Config;
using DarkNights.Core.Physics;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using UnityEngine;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 当前浮点格运动的实际后端，统一选择船舱、地形、平台和飞行路径，不另设调度或坐标状态。
    /// 实例随MovementBehaviour激活创建、归池释放；只在所属ActorState的现有写事务内消费一次冻结意图。
    /// 船舱出口以已处理的水平位移接续本步纵向运动，不回调角色控制器或重新提交输入。
    /// </summary>
    internal sealed class GridActorMotionBackend
    {
        private readonly ActorBehaviour actor;
        private readonly MovementDefinition movement;
        internal bool CanJump => movement.CanJump;

        internal GridActorMotionBackend(ActorBehaviour actor, MovementDefinition movement)
        {
            this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
            this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
        }

        internal bool Step(ActorMotionInput input)
        {
            if (input.ActorId != actor.Id)
                throw new InvalidOperationException("Movement input belongs to another actor.");
            var state = actor.Edit();
            float previousX = state.X;
            if (actor.World.Ship.Cabin.Player(actor, this, input)) return true;
            if (input.FlightHeight.HasValue) StepFlight(input);
            else StepWorld(input, input.JumpPressed, input.DropPressed, input.JumpHeld);
            state.Walking = Math.Abs(state.X - previousX) > .001f;
            return false;
        }

        internal float MoveSpeed(ActorMotionInput input) => actor.Read().ManualControl
            ? actor.World.Catalog.Balance.HeroControl.MoveSpeed(input.SprintHeld) * actor.World.DebugHeroSpeedMultiplier
            : input.NormalSpeed ?? (float)actor.Definition.Speed;

        internal float HorizontalTarget(ActorMotionInput input, bool grounded) => MovementHorizontalMotion.Target(
            actor.Edit(), actor.World.Catalog.Balance.HeroControl, movement, grounded, input.DeltaSeconds,
            actor.World.Layout.WorldWidth - 16, actor.World.DebugHeroSpeedMultiplier,
            input.Direction, input.NormalSpeed, input.GoalX);

        internal void LandHorizontal(ActorMotionInput input) => MovementHorizontalMotion.Land(
            actor.Edit(), actor.World.Catalog.Balance.HeroControl, actor.World.DebugHeroSpeedMultiplier,
            input.Direction, input.NormalSpeed);

        internal void ContinueFromCabin(ActorMotionInput input, float appliedX) =>
            StepWorld(input, false, false, actor.Read().JumpHeld, appliedX);

        private void StepWorld(ActorMotionInput input, bool jump, bool drop, bool held, float? appliedX = null)
        {
            var world = actor.World;
            HeroControlDefinition rules = world.Catalog.Balance.HeroControl;
            if (rules == null) return;
            if (world.Terrain == null)
            {
                PlatformMovement.Tick(actor, movement, input.DeltaSeconds, jump, drop, held,
                    input.Direction, input.NormalSpeed, input.GoalX, appliedX);
                return;
            }
            var flow = world.Flow;
            var planet = flow?.Enabled == true && !flow.IsSpace ? flow.ActivePlanet : null;
            float ceiling = planet == null ? rules.MaximumHeight :
                Math.Max(rules.MaximumHeight, planet.DockHeight + rules.MaximumHeight);
            TerrainHeroMotion.Tick(world.Terrain.Map, actor.Edit(), rules, input.DeltaSeconds, jump, held,
                ceiling, appliedX, inputPrepared: true, jetpack: false, movement: movement,
                speedMultiplier: world.DebugHeroSpeedMultiplier, maximumX: world.Layout.WorldWidth - 16,
                direction: input.Direction, normalSpeed: input.NormalSpeed, goalX: input.GoalX);
        }

        private void StepFlight(ActorMotionInput input)
        {
            if (input.DeltaSeconds == 0) return;
            var state = actor.Edit();
            float delta = (float)input.DeltaSeconds;
            Vector2 next = Vector2.MoveTowards(new Vector2(state.X, state.Height),
                new Vector2(input.GoalX ?? state.X, input.FlightHeight.Value), MoveSpeed(input) * delta);
            for (float y = 1; y <= 14; y += 3)
                if (TerrainHeroMotion.Solid(actor.World.Terrain.Map, next.x - 5, next.y + y) ||
                    TerrainHeroMotion.Solid(actor.World.Terrain.Map, next.x + 5, next.y + y)) return;
            state.HorizontalSpeed = (next.x - state.X) / delta;
            state.VerticalSpeed = (next.y - state.Height) / delta;
            state.TakeoffHorizontalSpeed = 0;
            state.X = next.x;
            state.Height = next.y;
        }
    }
}
