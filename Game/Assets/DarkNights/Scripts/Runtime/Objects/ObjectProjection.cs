using System;
using System.Linq;
using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 从当前 YYGC Behaviour 状态冻结一次完整展示帧；只读映射不消耗随机数或执行玩法。
    /// Host 与客户端共享相同身份合同，投影不携带恢复计时或池引用。
    /// </summary>
    internal static class ObjectProjection
    {
        internal static WorldViewData Capture(ObjectSession session)
        {
            var camp = session.Camp.Read();
            var economy = session.Economy.Read();
            var actors = session.Index.Actors.Select(actor =>
            {
                ActorState a = actor.Read();
                return new ActorViewData(a.Id, actor.RuleKey, a.Name, a.Enemy, a.X, a.Hp,
                    a.Activity.ToString(), a.TargetId, a.Face, a.Walking, a.ActionTime, a.Windup, a.HitFlash);
            }).ToArray();
            var buildings = session.Index.Buildings.Select(building =>
            {
                BuildingState b = building.Read();
                return new BuildingViewData(b.Id, building.RuleKey, b.X, b.Hp, b.Progress,
                    b.WorkerId, b.FarmSiteId, b.HitFlash, Array.Empty<TrainingViewData>());
            }).ToArray();
            var sites = session.Index.Worksites.Select(site =>
            {
                WorksiteState w = site.Read();
                return new WorksiteViewData(w.Id, site.RuleKey, w.X, w.WorkerId, w.Amount, w.Progress, w.Variant, w.FarmId);
            }).ToArray();
            var summary = new CampViewData(session.Economy.Stock, session.Economy.Population, session.Economy.Capacity,
                economy.RecruitCooldown, 0, "Day", session.Catalog.Level.Waves[0].DaySeconds,
                session.Index.EnemyCount, camp.Mode.ToString(), camp.Kills, camp.Lost, session.Economy.Gathered);
            var identities = session.Index.FreezeOrder().Select(entity =>
                new EntityIdentityData(entity.Id, entity.DefinitionGuid, entity.PlacementKey)).ToArray();
            return new WorldViewData(summary, actors, buildings, sites, Array.Empty<ProjectileViewData>(), identities);
        }
    }
}
