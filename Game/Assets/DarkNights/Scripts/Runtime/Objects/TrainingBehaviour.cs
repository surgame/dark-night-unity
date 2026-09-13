using System;
using System.Linq;
using DarkNights.Core.Logic.State;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 兵营装配的训练能力，付费队列与计时始终写所属 BuildingState。
    /// 工人到达后仅推进队首，完成时在同一事务替换职业，保留 EntityId 与原插入位置。
    /// </summary>
    public sealed partial class TrainingBehaviour : PooledBehaviour, IBuildingActivity
    {
        [Inject] private BuildingBehaviour building;

        protected override void OnSpawn()
        {
            if (building == null || building.RuleKey != "barracks")
                throw new InvalidOperationException("Training requires the barracks building rule.");
        }

        public void Tick(double delta)
        {
            BuildingState state = building.Read();
            if (state.TrainingQueue.Length == 0) return;
            ObjectSession session = building.World;
            TrainingStateEntry entry = state.TrainingQueue[0];
            ActorBehaviour worker = session.Index.Find<ActorBehaviour>(entry.ActorId);
            if (worker == null)
            {
                building.Edit().TrainingQueue = state.TrainingQueue.Skip(1).ToArray();
                return;
            }
            if (worker.Activity != ActorActivity.Training) return;
            state = building.Edit();
            entry.Remaining = Math.Max(0, entry.Remaining - delta);
            state.TrainingQueue[0] = entry;
            if (entry.Remaining > 0) return;
            ActorBehaviour promoted = session.Lifecycle.Promote(worker, entry.Kind, building.X + building.Definition.Width * 0.5f + 9);
            state.TrainingQueue = state.TrainingQueue.Skip(1).ToArray();
            session.Notify(promoted.Name + "完成训练：" + promoted.Definition.Name + "。");
            session.Mutations.AfterCommit(() => session.Feedback.PlaySound("snd_training_complete"));
        }
    }
}
