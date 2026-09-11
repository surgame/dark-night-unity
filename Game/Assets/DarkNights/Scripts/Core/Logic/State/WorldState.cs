using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Logic.Entities;

namespace DarkNights.Core.Logic.State
{
    /// <summary>
    /// 拥有本局实体集合、稳定ID注册及待结算箭矢。对外提供只读集合；工厂和生命周期服务负责增删，模拟迭代使用快照避免死亡时破坏枚举。
    /// </summary>
    public sealed class WorldState
    {
        private readonly List<Actor> _actors = new List<Actor>();
        private readonly List<Building> _buildings = new List<Building>();
        private readonly List<Worksite> _worksites = new List<Worksite>();
        private readonly Dictionary<int, Entity> _entities = new Dictionary<int, Entity>();
        public IReadOnlyList<Actor> Actors { get; }
        public IReadOnlyList<Building> Buildings { get; }
        public IReadOnlyList<Worksite> Worksites { get; }
        public List<Projectile> Projectiles { get; } = new List<Projectile>();
        public int NextId { get; internal set; } = 1;
        public int EnemyCount => _actors.Count(a => a.Enemy && a.Hp > 0);

        public WorldState()
        {
            Actors = _actors.AsReadOnly();
            Buildings = _buildings.AsReadOnly();
            Worksites = _worksites.AsReadOnly();
        }

        public Entity Find(int id) => _entities.GetValueOrDefault(id);

        public T Find<T>(int id) where T : Entity => Find(id) as T;

        internal int Allocate(int requested = 0)
        {
            int id = requested == 0 ? NextId : requested;
            NextId = Math.Max(NextId, id + 1);
            return id;
        }

        internal void Add(Entity entity)
        {
            _entities.Add(entity.Id, entity);
            switch (entity)
            {
                case Actor actor:
                    _actors.Add(actor);
                    break;
                case Building building:
                    _buildings.Add(building);
                    break;
                case Worksite site:
                    _worksites.Add(site);
                    break;
            }
        }

        internal void Remove(Entity entity)
        {
            _entities.Remove(entity.Id);
            switch (entity)
            {
                case Actor actor:
                    _actors.Remove(actor);
                    break;
                case Building building:
                    _buildings.Remove(building);
                    break;
                case Worksite site:
                    _worksites.Remove(site);
                    break;
            }
        }

        internal void Clear()
        {
            _actors.Clear();
            _buildings.Clear();
            _worksites.Clear();
            _entities.Clear();
            Projectiles.Clear();
            NextId = 1;
        }
    }
}
