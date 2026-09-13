using DarkNights.Core.ViewData;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 工人、长矛兵和敌人的近战命中实现，命中时由单位战斗能力提供已抽取伤害。
    /// 不自行推进攻击时钟，伤害和声音都属于当前会话事务。
    /// </summary>
    public sealed partial class MeleeAttackBehaviour : PooledBehaviour, IAttackCapability
    {
        [Inject] private ActorBehaviour actor;

        public void Hit(ICombatantCapability target, int damage)
        {
            ObjectSession session = actor.World;
            session.Combat.Damage(target, damage);
            session.Mutations.AfterCommit(() => session.Feedback.PlaySound("snd_hit_fleshy_light1", -16));
        }
    }
}
