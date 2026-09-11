using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Entities;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Logic.Commands
{
    /// <summary>
    /// 维护工人与工位/工地的双向独占关系。先检查新目标可用性再释放旧任务，死亡、换命令和完工共享同一清理入口。
    /// </summary>
    public sealed class WorkOrders
    {
        private readonly GameSession session;

        public WorkOrders(GameSession session)
        {
            this.session = session;
        }

        public void Release(int actorId)
        {
            foreach (var site in session.World.Worksites)
                if (site.WorkerId == actorId)
                {
                    site.WorkerId = 0;
                    site.Progress = 0;
                }
            foreach (var building in session.World.Buildings)
                if (building.WorkerId == actorId)
                    building.WorkerId = 0;
        }

        public void Clear(Actor actor)
        {
            Release(actor.Id);
            actor.State = ActorActivity.Idle;
            actor.TargetId = 0;
            actor.HitPending = false;
            actor.ForcedAttack = false;
            actor.MoveX = actor.X;
            actor.ActionTime = 0;
        }

        public bool Assign(Actor worker, Entity workplace)
        {
            if (worker == null || workplace == null || !ReferenceEquals(session.World.Find(worker.Id), worker) ||
                !ReferenceEquals(session.World.Find(workplace.Id), workplace))
                return false;
            if (worker.Kind != "worker" || worker.Enemy || worker.IsTraining || worker.Hp <= 0)
                return false;
            switch (workplace)
            {
                case Worksite site when site.Amount != 0 && (site.WorkerId == 0 || site.WorkerId == worker.Id):
                    Clear(worker);
                    site.WorkerId = worker.Id;
                    worker.State = ActorActivity.WorkMove;
                    break;
                case Building building when !building.IsComplete && (building.WorkerId == 0 || building.WorkerId == worker.Id):
                    Clear(worker);
                    building.WorkerId = worker.Id;
                    worker.State = ActorActivity.BuildMove;
                    break;
                default:
                    return false;
            }
            worker.TargetId = workplace.Id;
            return true;
        }
    }
}
