using System;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Terrain;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 玩家与自动单位唯一的运动执行入口；控制器只提交意图，每帧由ActorBehaviour调度一次。
    /// 跳跃、空控、地形与船舱统一消费冻结MovementConfig，所有持久量仍归ActorState。
    /// 目标及路径请求只活到本次Tick结束，既不保存也不形成第二套坐标状态。
    /// </summary>
    [RequireConfig(typeof(MovementConfig))]
    public sealed partial class MovementBehaviour : PooledBehaviour, IMovementCapability
    {
        [Inject] private ActorBehaviour actor;
        [Inject] private MovementConfig config;
        private MovementDefinition definition;
        private float? goalX;
        private float? flightHeight;
        private float? requestedSpeed;
        private bool jumpRequested;
        internal bool HandledByCabin { get; private set; }
        internal bool CanJump => definition.CanJump;
        internal float Direction { get; private set; }
        internal float? NormalSpeed { get; private set; }
        internal float MoveSpeed => actor.Read().ManualControl
            ? actor.World.Catalog.Balance.HeroControl.MoveSpeed(actor.Read().SprintHeld) * actor.World.DebugHeroSpeedMultiplier
            : NormalSpeed ?? (float)actor.Definition.Speed;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            if (actor == null || config == null) throw new InvalidOperationException("Movement requires actor and configuration.");
            definition = config.Freeze();
            goalX = flightHeight = requestedSpeed = null;
            jumpRequested = HandledByCabin = false;
            Direction = 0;
            NormalSpeed = null;
        }

        public bool MoveTo(float x, double delta)
        {
            if (float.IsNaN(x) || float.IsInfinity(x)) throw new ArgumentException("Movement target must be finite.");
            goalX = Math.Clamp(x, 16, actor.World.Layout.WorldWidth - 16);
            return Math.Abs(actor.X - goalX.Value) < definition.ArrivalDistance;
        }

        internal void FollowPath(float x, float height, bool jump, bool flying)
        {
            MoveTo(x, 0);
            requestedSpeed = (float)Math.Max(30, actor.Definition.Speed) * (flying ? 2 : 1);
            flightHeight = flying ? height : (float?)null;
            jumpRequested = jump;
        }

        public void Tick(double delta)
        {
            ActorState state = actor.Edit();
            float previousX = state.X;
            try
            {
                Direction = state.ManualControl ? state.Horizontal : goalX.HasValue ? Math.Sign(goalX.Value - state.X) : 0;
                NormalSpeed = state.ManualControl ? null : requestedSpeed ?? (float)actor.Definition.Speed;
                if (Direction != 0) state.Face = Math.Sign(Direction);
                bool jump = definition.CanJump && (state.ManualControl ? state.JumpPending : jumpRequested);
                HeroJumpMotion.Sample(state, jump, delta);
                if (!definition.CanJump) state.JumpBufferRemaining = 0;
                HandledByCabin = actor.World.Ship.Cabin.Player(actor, this, delta);
                if (HandledByCabin) return;
                if (flightHeight.HasValue) StepFlight(state, delta);
                else StepWorld(delta, jump, state.ManualControl && state.DropPending, state.ManualControl && state.JumpHeld);
                state.Walking = Math.Abs(state.X - previousX) > .001f;
            }
            finally
            {
                state.JumpPending = state.DropPending = false;
                goalX = flightHeight = requestedSpeed = null;
                jumpRequested = false;
            }
        }

        internal float HorizontalTarget(double delta, bool grounded) => MovementHorizontalMotion.Target(actor.Edit(),
            actor.World.Catalog.Balance.HeroControl, definition, grounded, delta, actor.World.Layout.WorldWidth - 16,
            actor.World.DebugHeroSpeedMultiplier, Direction, NormalSpeed, goalX);

        internal void LandHorizontal() => MovementHorizontalMotion.Land(actor.Edit(),
            actor.World.Catalog.Balance.HeroControl, actor.World.DebugHeroSpeedMultiplier, Direction, NormalSpeed);

        internal void ContinueFromCabin(double delta, float appliedX) =>
            StepWorld(delta, false, false, actor.Read().JumpHeld, appliedX);

        private void StepWorld(double delta, bool jump, bool drop, bool held, float? appliedX = null)
        {
            HeroControlDefinition rules = actor.World.Catalog.Balance.HeroControl;
            if (rules == null) return;
            if (actor.World.Terrain == null)
            {
                PlatformMovement.Tick(actor, definition, delta, jump, drop, held,
                    Direction, NormalSpeed, goalX, appliedX);
                return;
            }
            var flow = actor.World.Flow;
            var planet = flow?.Enabled == true && !flow.IsSpace ? flow.ActivePlanet : null;
            float ceiling = planet == null ? rules.MaximumHeight : Math.Max(rules.MaximumHeight, planet.DockHeight + rules.MaximumHeight);
            Terrain.TerrainHeroMotion.Tick(actor.World.Terrain.Map, actor.Edit(), rules, delta, jump, held, ceiling, appliedX,
                inputPrepared: true, jetpack: false, movement: definition,
                speedMultiplier: actor.World.DebugHeroSpeedMultiplier, maximumX: actor.World.Layout.WorldWidth - 16,
                direction: Direction, normalSpeed: NormalSpeed, goalX: goalX);
        }

        private void StepFlight(ActorState state, double delta)
        {
            Vector2 next = Vector2.MoveTowards(new Vector2(state.X, state.Height),
                new Vector2(goalX ?? state.X, flightHeight.Value), MoveSpeed * (float)delta);
            for (float y = 1; y <= 14; y += 3)
                if (Terrain.TerrainHeroMotion.Solid(actor.World.Terrain.Map, next.x - 5, next.y + y) ||
                    Terrain.TerrainHeroMotion.Solid(actor.World.Terrain.Map, next.x + 5, next.y + y)) return;
            state.HorizontalSpeed = (next.x - state.X) / (float)delta;
            state.VerticalSpeed = (next.y - state.Height) / (float)delta;
            state.TakeoffHorizontalSpeed = 0;
            state.X = next.x;
            state.Height = next.y;
        }
    }
}
