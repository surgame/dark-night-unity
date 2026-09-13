using System;
using DarkNights.Core.Logic;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 复用 ActorBehaviour 的唯一状态推进移动，能力本身不存坐标或移动计时。
    /// 读取生成注入的规则和到达配置；没有此能力的单位不能通过原型装配检查。
    /// </summary>
    [RequireConfig(typeof(MovementConfig))]
    public sealed partial class MovementBehaviour : PooledBehaviour, IMovementCapability
    {
        [Inject] private ActorBehaviour actor;
        [Inject] private MovementConfig config;

        protected override void OnSpawn()
        {
            if (actor == null || config == null || float.IsNaN(config.ArrivalDistance) ||
                config.ArrivalDistance <= 0 || config.ArrivalDistance > 4)
                throw new InvalidOperationException("Movement requires an actor and a valid arrival distance.");
        }

        public bool MoveTo(float x, double delta)
        {
            ActorState state = actor.Edit();
            if (Math.Abs(state.X - x) < config.ArrivalDistance)
            {
                state.X = x;
                return true;
            }
            state.Face = Math.Sign(x - state.X);
            state.X = (float)SimulationMath.MoveToward((double)state.X, x, actor.Definition.Speed * delta);
            state.Walking = true;
            return Math.Abs(state.X - x) < config.ArrivalDistance;
        }
    }
}
