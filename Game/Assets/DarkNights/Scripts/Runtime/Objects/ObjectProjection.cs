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
                    a.Activity.ToString(), a.TargetId, a.Face, a.Walking, a.ActionTime, a.Windup, a.HitFlash,
                    a.Height, a.VerticalSpeed, a.SupportPlatform, a.ManualControl, a.SelectedItem, a.SelectionRevision, a.JetpackEquipped, a.JetpackFuel, a.ControllerSlot, a.ControlLease, a.ExplosiveCharges, a.AimAngle, a.EquipmentCooldown, a.EquipmentAction, a.EquipmentActionDuration, a.Charging, a.ChargeSeconds);
            }).ToArray();
            var buildings = session.Index.Buildings.Select(building =>
            {
                BuildingState b = building.Read();
                return new BuildingViewData(b.Id, building.RuleKey, b.X, b.Hp, b.Progress,
                    b.WorkerId, b.FarmSiteId, b.HitFlash,
                    b.TrainingQueue.Select(t => new TrainingViewData(t.ActorId, t.Kind, t.Remaining)).ToArray());
            }).ToArray();
            var sites = session.Index.Worksites.Select(site =>
            {
                WorksiteState w = site.Read();
                return new WorksiteViewData(w.Id, site.RuleKey, w.X, w.WorkerId, w.Amount, w.Progress, w.Variant, w.FarmId);
            }).Concat(session.Index.MineralDeposits.Select(deposit =>
            {
                return new WorksiteViewData(deposit.Id, "mineral-deposit", deposit.X, deposit.Y, 0, deposit.Remaining, 0, 0, 0,
                    true, deposit.RoomKind, deposit.Rarity, deposit.Capacity, deposit.Stage.ToString());
            })).ToArray();
            WaveState wave = session.Waves.Read();
            var summary = new CampViewData(session.Economy.Stock, session.Economy.Population, session.Economy.Capacity,
                economy.RecruitCooldown, wave.Index, wave.Phase.ToString(), wave.DayRemaining,
                session.Index.EnemyCount, camp.Mode.ToString(), camp.Kills, camp.Lost, session.Economy.Gathered, wave.NextSpawn);
            var identities = session.Index.FreezeOrder().Select(entity =>
                new EntityIdentityData(entity.Id, entity.DefinitionGuid, entity.PlacementKey)).ToArray();
            var shots = session.Projectiles.Read().Shots.Select(p => new ProjectileViewData(
                p.ViewId, p.FromX, p.FromY, p.ToX, p.ToY, p.Age, p.Duration)).Concat(
                session.Projectiles.Read().Ballistics.Where(p => p.Kind != 0).Select(p => new ProjectileViewData(
                    p.ViewId, p.X, session.Layout.GroundY - p.Height, p.X, session.Layout.GroundY - p.Height,
                    p.Age, p.Lifetime, p.Kind, p.VelocityX, p.VelocityY, p.Gravity, p.Kind == 3 ? p.BlastRadius : p.Radius, p.Stuck))).ToArray();
            return new WorldViewData(summary, actors, buildings, sites, shots, identities);
        }
    }
}
