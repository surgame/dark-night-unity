using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 协调 YYGC 对象的出生、死亡及转职，不拥有另一套实体或可写状态。
    /// 索引立即反映本 tick 的增删，工厂与状态失败统一回滚，旧对象在事务成功后退休。
    /// </summary>
    public sealed class ObjectEntityLifecycle
    {
        private readonly ObjectSession session;
        internal ObjectEntityLifecycle(ObjectSession session) { this.session = session; }

        internal ActorBehaviour SpawnActor(string kind, float x, bool enemy = false, string name = "") =>
            (ActorBehaviour)session.Create(session.Resources.Find(kind), x, "", true, 0, name, null, enemy);

        internal WorksiteBehaviour SpawnSite(string kind, float x, int variant = 0, int farmId = 0) =>
            (WorksiteBehaviour)session.Create(session.Resources.Find(kind), x, "", true, variant, "", null, false, farmId);

        internal ActorBehaviour Promote(ActorBehaviour worker, string kind, float x)
        {
            session.Mutations.RequireWriting();
            ObjectInstance candidate = session.Resources.Create(session.Resources.Find(kind), session.EntityContext, session.Parent).Owner;
            int id = worker.Id;
            bool replaced = false;
            session.Mutations.OnRollback(() =>
            {
                if (replaced) session.Index.Replace(id, worker);
                session.ReleaseEntity(candidate);
            });
            session.Work.Clear(worker);
            ActorBehaviour actor = candidate.GetBehaviour<ActorBehaviour>();
            var state = new ActorState();
            state.CopyFrom(worker.Read());
            state.Hp = actor.Definition.Hp;
            state.X = state.RallyX = x;
            actor.PrepareState(state);
            candidate.Activate();
            candidate.gameObject.SetActive(false);
            session.Index.Replace(id, actor);
            replaced = true;
            ObjectInstance previous = worker.Object;
            session.Mutations.AfterCommit(() =>
            {
                session.ReleaseEntity(previous);
                candidate.gameObject.SetActive(true);
            });
            return actor;
        }

        internal void ActorDied(ActorBehaviour actor)
        {
            session.Work.Release(actor.Id);
            foreach (BuildingBehaviour building in session.Index.Buildings)
                if (building.Read().TrainingQueue.Any(entry => entry.ActorId == actor.Id))
                    building.Edit().TrainingQueue = building.Read().TrainingQueue.Where(entry => entry.ActorId != actor.Id).ToArray();
            if (actor.Enemy)
            {
                session.Camp.Edit().Kills++;
                session.Economy.Credit(new ResourceAmounts(gold: actor.Definition.Gold));
            }
            else
            {
                session.Camp.Edit().Lost++;
                session.Notify(actor.Name + "倒下了。", true);
            }
            session.Emit(new VisualCue("corpse", actor.X, session.Layout.GroundY - actor.Read().Height, ContentId: actor.RuleKey, Face: actor.Read().Face));
            string sound = actor.Enemy ? "snd_zombie_die1" : "snd_worker_die1";
            session.Mutations.AfterCommit(() => session.Feedback.PlaySound(sound, -14));
            Remove(actor);
        }

        internal void BuildingDestroyed(BuildingBehaviour building)
        {
            foreach (TrainingStateEntry entry in building.Read().TrainingQueue)
            {
                ActorBehaviour actor = session.Index.Find<ActorBehaviour>(entry.ActorId);
                if (actor == null) continue;
                session.Work.Clear(actor);
                session.Economy.Credit(session.Catalog.Balance.Units[entry.Kind].Cost);
            }
            WorksiteBehaviour site = session.Index.Find<WorksiteBehaviour>(building.FarmSiteId);
            if (site != null) Remove(site);
            if (building.RuleKey != "farm")
                session.Emit(new VisualCue("rubble", building.X, session.Layout.GroundY, ContentId: building.RuleKey));
            session.Notify(building.Definition.Name + "被摧毁。", true);
            Remove(building);
            if (building.RuleKey == "tavern") session.Camp.Finish(false);
        }

        private void Remove(IEntityBehaviour entity)
        {
            int id = entity.Id;
            ObjectInstance owner = entity.Object;
            session.Index.Remove(entity, session.Mutations);
            foreach (ActorBehaviour actor in session.Index.Actors)
                if (actor.TargetId == id) session.Work.Clear(actor);
            session.Mutations.AfterCommit(() => session.ReleaseEntity(owner));
        }
    }
}
