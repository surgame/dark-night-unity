using System;
using DarkNights.Core.Logic.State;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 从原 ActorBehaviour 原序提取的营地自动控制，可通过 ObjectDefinition 装配替换。
    /// 工作、训练、移动和战斗只读写所属 ActorState，主角接管时不执行此能力。
    /// </summary>
    public sealed partial class AutomaticActorControlBehaviour : PooledBehaviour, IAutomaticActorControl
    {
        [Inject] private ActorBehaviour actor;
        [Inject] private IMovementCapability movement;
        [Inject] private IActorCombatCapability combat;
        public void Tick(double delta)
        {
            ActorState state = actor.Edit();
            if (actor.IsTraining)
            {
                BuildingBehaviour barracks = actor.World.Index.Find<BuildingBehaviour>(state.TargetId);
                if (barracks == null) actor.World.Work.Clear(actor);
                else if (state.Activity == ActorActivity.TrainingMove && movement.MoveTo(barracks.X + 10, delta))
                    state.Activity = ActorActivity.Training;
                return;
            }
            if (state.Activity == ActorActivity.WorkMove || state.Activity == ActorActivity.Work ||
                state.Activity == ActorActivity.BuildMove || state.Activity == ActorActivity.Build)
            {
                IEntityBehaviour workplace = actor.World.Index.Find(state.TargetId);
                int owner = workplace is WorksiteBehaviour site ? site.WorkerId :
                    workplace is BuildingBehaviour building ? building.WorkerId : 0;
                if (workplace == null || owner != actor.Id) actor.World.Work.Clear(actor);
                else if (state.Activity == ActorActivity.WorkMove || state.Activity == ActorActivity.BuildMove)
                {
                    if (movement.MoveTo(workplace.X - 10, delta))
                    {
                        state.Activity = state.Activity == ActorActivity.WorkMove ? ActorActivity.Work : ActorActivity.Build;
                        state.ActionTime = 0;
                    }
                }
                else state.Face = 1;
            }
            if (state.Activity == ActorActivity.Move)
            {
                if (movement.MoveTo(state.MoveX, delta)) state.Activity = ActorActivity.Idle;
            }
            else if (state.AiClock <= 0)
            {
                state.AiClock = 0.25;
                combat.FindTarget();
            }
            if (state.Activity == ActorActivity.Attack) combat.TickAttack(delta);
        }
    }
}
