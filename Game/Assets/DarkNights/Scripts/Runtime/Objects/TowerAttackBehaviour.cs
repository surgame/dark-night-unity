using System;
using DarkNights.Core.Logic;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 守望塔完工后的攻击能力，按建筑阶段顺序索敌和发射会话箭矢。
    /// 冷却写建筑唯一状态，射程、间隔和伤害来自当前 RuleKey 的只读规则。
    /// </summary>
    public sealed partial class TowerAttackBehaviour : PooledBehaviour, IBuildingActivity
    {
        [Inject] private BuildingBehaviour building;

        protected override void OnSpawn()
        {
            if (building == null || building.RuleKey != "tower")
                throw new InvalidOperationException("Tower attack requires the tower building rule.");
        }

        public void Tick(double delta)
        {
            if (building.Read().AttackClock > 0) return;
            ObjectSession session = building.World;
            ActorBehaviour target = session.Combat.NearestEnemy(building.X, building.Definition.Range);
            if (target == null) return;
            building.Edit().AttackClock = building.Definition.AttackSeconds;
            int damage = session.Camp.RandomInt(building.Definition.Damage[0], building.Definition.Damage[1]);
            session.Projectiles.Launch(new WorldPoint(building.X, session.Layout.GroundY - 38), target, damage);
        }
    }
}
