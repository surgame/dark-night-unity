using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Entities;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Logic.Systems
{
    /// <summary>
    /// 集中创建、登记和移除运行实体，维护死亡后的引用与训练退款。场景节点不参与此流程，存档恢复也通过相同工厂重建稳定身份。
    /// </summary>
    public sealed class EntityLifecycle
    {
        private readonly GameSession session;

        public EntityLifecycle(GameSession session)
        {
            this.session = session;
        }

        public Actor SpawnActor(string kind, float x, bool enemy = false, string name = "", int requestedId = 0)
        {
            var actor = new Actor(session, session.World.Allocate(requestedId), kind, x, enemy, name);
            session.World.Add(actor);
            return actor;
        }

        public Building SpawnBuilding(string kind, float x, bool complete = false, int requestedId = 0, bool createFarm = true)
        {
            var building = new Building(session, session.World.Allocate(requestedId), kind, x, complete);
            session.World.Add(building);
            if (complete && kind == "farm" && createFarm)
                building.FarmSiteId = SpawnSite("food", x, 0, building.Id).Id;
            return building;
        }

        public Worksite SpawnSite(string kind, float x, int variant = 0, int farmId = 0, int requestedId = 0)
        {
            var site = new Worksite(session, session.World.Allocate(requestedId), kind, x, variant, farmId);
            session.World.Add(site);
            return site;
        }

        internal void ActorDied(Actor actor)
        {
            session.Work.Release(actor.Id);
            foreach (var building in session.World.Buildings)
                building.TrainingQueue.RemoveAll(e => e.ActorId == actor.Id);
            if (actor.Enemy)
            {
                session.Stats.Kills++;
                session.Economy.Credit(new ResourceAmounts(gold: actor.Definition.Gold));
            }
            else
            {
                session.Stats.Lost++;
                session.Feedback.Notify($"{actor.Name}倒下了。", true);
            }
            session.Feedback.Emit(new("corpse", actor.X, session.GroundY, ContentId: actor.Kind, Face: actor.Face));
            session.Feedback.PlaySound(actor.Enemy ? "snd_zombie_die1" : "snd_worker_die1", -14);
            Remove(actor);
        }

        internal void BuildingDestroyed(Building building)
        {
            foreach (var entry in building.TrainingQueue)
            {
                var actor = session.World.Find<Actor>(entry.ActorId);
                if (actor == null)
                    continue;
                actor.ClearOrder();
                session.Economy.Credit(session.Catalog.Balance.Units[entry.Kind].Cost);
            }
            var site = session.World.Find<Worksite>(building.FarmSiteId);
            if (site != null)
                Remove(site);
            if (building.Kind != "farm")
                session.Feedback.Emit(new("rubble", building.X, session.GroundY, ContentId: building.Kind));
            session.Feedback.Notify($"{building.Definition.Name}被摧毁。", true);
            Remove(building);
            if (building.Kind == "tavern")
                session.Finish(false);
        }

        private void Remove(Entity entity)
        {
            session.World.Remove(entity);
            foreach (var actor in session.World.Actors)
                if (actor.TargetId == entity.Id)
                    actor.ClearOrder();
        }
    }
}
