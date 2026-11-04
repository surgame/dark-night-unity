using System;
using DarkNights.Core.Config;
using DarkNights.Core.Physics;
using DarkNights.Runtime.Physics;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 玩家与自动单位唯一的运动执行入口；控制器只提交意图，每帧由ActorBehaviour调度一次。
    /// 冻结本步语义意图后交给实际格运动后端，跳跃和空控配置及持久量仍归原定义与ActorState。
    /// 目标及路径请求只活到本次Tick结束，既不保存也不形成第二套坐标状态。
    /// </summary>
    [RequireConfig(typeof(MovementConfig))]
    public sealed partial class MovementBehaviour : PooledBehaviour, IMovementCapability
    {
        [Inject] private ActorBehaviour actor;
        [Inject] private MovementConfig config;
        private MovementDefinition definition;
        private GridActorMotionBackend backend;
        private float? goalX;
        private float? flightHeight;
        private float? requestedSpeed;
        private bool jumpRequested;
        public bool HandledByCabin { get; private set; }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            if (actor == null || config == null) throw new InvalidOperationException("Movement requires actor and configuration.");
            definition = config.Freeze();
            backend = new GridActorMotionBackend(actor, definition);
            goalX = flightHeight = requestedSpeed = null;
            jumpRequested = HandledByCabin = false;
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
            try
            {
                float direction = state.ManualControl ? state.Horizontal : goalX.HasValue ? Math.Sign(goalX.Value - state.X) : 0;
                float? normalSpeed = state.ManualControl ? null : requestedSpeed ?? (float)actor.Definition.Speed;
                bool jump = definition.CanJump && (state.ManualControl ? state.JumpPending : jumpRequested);
                var input = new ActorMotionInput(actor.Id, delta, direction, jump,
                    state.ManualControl && state.DropPending, state.ManualControl && state.JumpHeld,
                    state.SprintHeld, goalX, flightHeight, normalSpeed, state.ManualControl ? state.Climb : 0);
                if (direction != 0) state.Face = Math.Sign(direction);
                HeroJumpMotion.Sample(state, jump, delta);
                if (!definition.CanJump) state.JumpBufferRemaining = 0;
                if (actor.World.Physics?.Enabled == true)
                {
                    actor.World.Physics.Submit(input, definition);
                    HandledByCabin = false;
                }
                else HandledByCabin = backend.Step(input);
            }
            finally
            {
                state.JumpPending = state.DropPending = false;
                goalX = flightHeight = requestedSpeed = null;
                jumpRequested = false;
            }
        }

        public override void OnDespawn()
        {
            backend = null;
            definition = null;
            goalX = flightHeight = requestedSpeed = null;
            jumpRequested = HandledByCabin = false;
            base.OnDespawn();
        }
    }
}
