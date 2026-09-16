using DarkNights.Core.Logic;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 弓箭手的发射能力；前摇结束后创建会话拥有的箭矢，动画与表现不结算伤害。
    /// 自身不保留目标和飞行状态，转职退休时没有额外订阅或计时器。
    /// </summary>
    public sealed partial class ArrowAttackBehaviour : PooledBehaviour, IAttackCapability
    {
        [Inject] private ActorBehaviour actor;

        public void Hit(ICombatantCapability target, int damage) => actor.World.Projectiles.Launch(
            new WorldPoint(actor.X, actor.World.Layout.GroundY - actor.Read().Height - 10), target, damage);
    }
}
