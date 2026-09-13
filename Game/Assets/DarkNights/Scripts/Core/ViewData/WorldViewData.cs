using System;
using System.Collections.Generic;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 一次模拟切点的完整、不可变展示集合，复制列表并验证总量及跨类别身份唯一性。
    /// 不持有 WorldState、恢复快照或池对象。上限超出时明确失败，不能截掉实体；内容字段须由权威映射或接收适配验证。
    /// </summary>
    public sealed class WorldViewData
    {
        public const int MaximumEntities = 256;
        public const int MaximumProjectiles = 1024;
        public CampViewData Camp { get; }
        public IReadOnlyList<ActorViewData> Actors { get; }
        public IReadOnlyList<BuildingViewData> Buildings { get; }
        public IReadOnlyList<WorksiteViewData> Worksites { get; }
        public IReadOnlyList<ProjectileViewData> Projectiles { get; }
        public IReadOnlyList<EntityIdentityData> Identities { get; }

        public WorldViewData(CampViewData camp, IReadOnlyList<ActorViewData> actors,
            IReadOnlyList<BuildingViewData> buildings, IReadOnlyList<WorksiteViewData> worksites,
            IReadOnlyList<ProjectileViewData> projectiles, IReadOnlyList<EntityIdentityData> identities = null)
        {
            Camp = camp ?? throw new ArgumentNullException(nameof(camp));
            if (actors == null || buildings == null || worksites == null || projectiles == null)
                throw new ArgumentNullException(nameof(actors));
            if ((long)actors.Count + buildings.Count + worksites.Count > MaximumEntities ||
                projectiles.Count > MaximumProjectiles) throw new ArgumentOutOfRangeException(nameof(actors), "Projection exceeds supported limits.");
            var ids = new HashSet<long>();
            Actors = Copy(actors, a => a.Id, ids);
            Buildings = Copy(buildings, b => b.Id, ids);
            Worksites = Copy(worksites, w => w.Id, ids);
            Projectiles = Copy(projectiles, p => p.ViewId, new HashSet<long>());
            Identities = Copy(identities ?? Array.Empty<EntityIdentityData>(), i => i.Id, new HashSet<long>());
            if (Identities.Count != 0 && (Identities.Count != ids.Count ||
                System.Linq.Enumerable.Any(Identities, i => !ids.Contains(i.Id))))
                throw new ArgumentException("Projection identity map must cover the whole world.");
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source, Func<T, long> identity, HashSet<long> ids) where T : class
        {
            var result = new List<T>(source.Count);
            foreach (T item in source)
            {
                if (item == null || identity(item) <= 0 || !ids.Add(identity(item)))
                    throw new ArgumentException("Projection contains null, invalid or duplicate identity.");
                result.Add(item);
            }
            return result.AsReadOnly();
        }
    }
}
