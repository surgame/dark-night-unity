using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 在一个会话事务里维护工人和工位或工地的双向独占关系，不拥有自己的订单副本。
    /// 先检查新目标，后释放旧任务；群体派工和移动保留原选择顺序及位置间距。
    /// </summary>
    public sealed class ObjectWorkOrders
    {
        private readonly ObjectSession session;
        internal ObjectWorkOrders(ObjectSession session) { this.session = session; }

        internal void Release(int actorId)
        {
            foreach (WorksiteBehaviour site in session.Index.Worksites)
                if (site.WorkerId == actorId)
                {
                    WorksiteState state = site.Edit();
                    state.WorkerId = 0;
                    state.Progress = 0;
                }
            foreach (BuildingBehaviour building in session.Index.Buildings)
                if (building.WorkerId == actorId) building.Edit().WorkerId = 0;
        }

        internal void Clear(ActorBehaviour actor)
        {
            Release(actor.Id);
            ActorState value = actor.Edit();
            value.Activity = ActorActivity.Idle;
            value.TargetId = 0;
            value.HitPending = value.ForcedAttack = false;
            value.MoveX = value.X;
            value.ActionTime = 0;
        }

        internal bool Assign(ActorBehaviour worker, IEntityBehaviour workplace)
        {
            if (worker == null || workplace == null || session.Index.Find(worker.Id) != worker ||
                session.Index.Find(workplace.Id) != workplace || worker.RuleKey != "worker" ||
                worker.Enemy || worker.Hp <= 0 || worker.IsTraining) return false;
            if (workplace is WorksiteBehaviour site && site.Amount != 0 &&
                (site.WorkerId == 0 || site.WorkerId == worker.Id))
            {
                Clear(worker);
                site.Edit().WorkerId = worker.Id;
                worker.Edit().Activity = ActorActivity.WorkMove;
            }
            else if (workplace is BuildingBehaviour building && !building.IsComplete &&
                (building.WorkerId == 0 || building.WorkerId == worker.Id))
            {
                Clear(worker);
                building.Edit().WorkerId = worker.Id;
                worker.Edit().Activity = ActorActivity.BuildMove;
            }
            else return false;
            worker.Edit().TargetId = workplace.Id;
            return true;
        }

        internal ActorBehaviour[] Resolve(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count > 256 || ids.Distinct().Count() != ids.Count) return null;
            var actors = ids.Select(id => session.Index.Find<ActorBehaviour>(id)).ToArray();
            return actors.Any(a => a == null || a.Enemy || a.Hp <= 0) ? null : actors;
        }

        internal int Issue(IReadOnlyList<int> ids, int targetId, float x)
        {
            ActorBehaviour[] selected = Resolve(ids);
            if (session.Camp.Read().Mode != SessionMode.Playing || float.IsNaN(x) || float.IsInfinity(x)) return 0;
            if (selected == null || selected.Length == 0) return 0;
            IEntityBehaviour target = session.Index.Find(targetId);
            if (targetId != 0 && target == null) return 0;
            if (target is BuildingBehaviour farm && farm.RuleKey == "farm" && farm.IsComplete)
                target = session.Index.Find(farm.FarmSiteId);
            int assigned = 0;
            foreach (ActorBehaviour actor in selected)
            {
                if (actor.IsTraining) continue;
                if (target is WorksiteBehaviour desired)
                {
                    if (actor.RuleKey != "worker") continue;
                    WorksiteBehaviour site = desired.WorkerId == 0 || desired.WorkerId == actor.Id ? desired :
                        session.Index.Worksites.Where(s => s.RuleKey == desired.RuleKey && s.WorkerId == 0 && s.Amount != 0)
                            .OrderBy(s => Math.Abs(s.X - desired.X)).FirstOrDefault();
                    if (site != null && Assign(actor, site)) assigned++;
                }
                else if (target is BuildingBehaviour building && !building.IsComplete)
                {
                    if (Assign(actor, building)) assigned++;
                }
                else
                {
                    if (target is ActorBehaviour enemy && enemy.Enemy)
                    {
                        Clear(actor);
                        actor.Edit().TargetId = enemy.Id;
                        actor.Edit().Activity = ActorActivity.Attack;
                        actor.Edit().ForcedAttack = true;
                        assigned++;
                        continue;
                    }
                    float offset = (assigned - (selected.Length - 1) * 0.5f) * 8;
                    if (actor.RuleKey == "archer" && selected.Length > 1) offset -= 32;
                    actor.OrderMove(x + offset);
                    assigned++;
                }
            }
            if (assigned > 0)
            {
                if (target is WorksiteBehaviour site)
                    session.Notify("已安排" + assigned + "名工人采集" + GameText.ResourceName(site.RuleKey) + "。");
            }
            else session.Notify("目标已被占用，或所选单位正在训练。工作和施工需要工人。", true);
            return assigned;
        }
    }
}
