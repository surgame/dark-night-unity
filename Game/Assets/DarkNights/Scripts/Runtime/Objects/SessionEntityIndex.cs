using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 当前营地内 EntityId 到 YYGC 业务能力的只读索引，稳定保存各家族的插入顺序。
    /// 不分配 ID、不拥有位置或生命状态；增删与回滚只改对象引用。
    /// </summary>
    public sealed class SessionEntityIndex
    {
        private readonly Dictionary<int, IEntityBehaviour> entities = new Dictionary<int, IEntityBehaviour>();
        private readonly List<ActorBehaviour> actors = new List<ActorBehaviour>();
        private readonly List<BuildingBehaviour> buildings = new List<BuildingBehaviour>();
        private readonly List<WorksiteBehaviour> worksites = new List<WorksiteBehaviour>();
        public IReadOnlyList<ActorBehaviour> Actors { get; }
        public IReadOnlyList<BuildingBehaviour> Buildings { get; }
        public IReadOnlyList<WorksiteBehaviour> Worksites { get; }
        public int Count => entities.Count;
        public int EnemyCount => actors.Count(a => a.Enemy && a.Hp > 0);

        public SessionEntityIndex()
        {
            Actors = actors.AsReadOnly();
            Buildings = buildings.AsReadOnly();
            Worksites = worksites.AsReadOnly();
        }

        public IEntityBehaviour Find(int id) => entities.TryGetValue(id, out var entity) ? entity : null;
        public T Find<T>(int id) where T : class, IEntityBehaviour => Find(id) as T;
        public IEntityBehaviour[] FreezeOrder() => buildings.Cast<IEntityBehaviour>().Concat(actors).Concat(worksites).ToArray();

        internal void Add(IEntityBehaviour entity)
        {
            if (entity == null || entity.Id <= 0 || entities.Count >= 256 || entities.ContainsKey(entity.Id))
                throw new InvalidOperationException("Invalid or duplicate entity registration.");
            if (entity.PlacementKey.Length != 0 && entities.Values.Any(e => e.PlacementKey == entity.PlacementKey))
                throw new InvalidOperationException("Duplicate live placement: " + entity.PlacementKey);
            entities.Add(entity.Id, entity);
            if (entity is ActorBehaviour actor) actors.Add(actor);
            else if (entity is BuildingBehaviour building) buildings.Add(building);
            else if (entity is WorksiteBehaviour site) worksites.Add(site);
            else throw new InvalidOperationException("Unknown YYGC entity family.");
        }

        internal void Remove(IEntityBehaviour entity)
        {
            if (!ReferenceEquals(Find(entity.Id), entity)) return;
            entities.Remove(entity.Id);
            if (entity is ActorBehaviour actor) actors.Remove(actor);
            else if (entity is BuildingBehaviour building) buildings.Remove(building);
            else if (entity is WorksiteBehaviour site) worksites.Remove(site);
        }

        internal void Remove(IEntityBehaviour entity, ObjectMutationBatch mutations)
        {
            if (!ReferenceEquals(Find(entity.Id), entity)) return;
            int position = entity is ActorBehaviour actor ? actors.IndexOf(actor) :
                entity is BuildingBehaviour building ? buildings.IndexOf(building) : worksites.IndexOf((WorksiteBehaviour)entity);
            int id = entity.Id;
            mutations.OnRollback(() =>
            {
                entities.Add(id, entity);
                if (entity is ActorBehaviour a) actors.Insert(position, a);
                else if (entity is BuildingBehaviour b) buildings.Insert(position, b);
                else worksites.Insert(position, (WorksiteBehaviour)entity);
            });
            Remove(entity);
        }

        internal void Clear()
        {
            entities.Clear();
            actors.Clear();
            buildings.Clear();
            worksites.Clear();
        }

        internal void Replace(int id, IEntityBehaviour replacement)
        {
            IEntityBehaviour previous = entities[id];
            entities[id] = replacement;
            if (previous is ActorBehaviour actor) actors[actors.IndexOf(actor)] = (ActorBehaviour)replacement;
            else if (previous is BuildingBehaviour building) buildings[buildings.IndexOf(building)] = (BuildingBehaviour)replacement;
            else if (previous is WorksiteBehaviour site) worksites[worksites.IndexOf(site)] = (WorksiteBehaviour)replacement;
        }
    }
}
