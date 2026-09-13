using System;
using System.Reflection;
using DarkNights.Core.Logic;
using DarkNights.Runtime.Objects;
using DarkNights.Core.Config;

namespace DarkNights.Tests
{
    /// <summary>
    /// 对真实 YYGC 会话施加战斗边界条件的测试入口，只反射内部方法，不伪造框架实现。
    /// 每次伤害或发射都经过正式事务，常规用户命令仍通过 ObjectSession 的公开请求入口。
    /// </summary>
    internal static class UnifiedGameplayProbe
    {
        internal static bool Pay(ObjectSession world, ResourceAmounts cost) => world.Mutations.Run(() =>
            (bool)Invoke(world.Economy, "Pay", cost));

        internal static bool Assign(ObjectSession world, ActorBehaviour actor, IEntityBehaviour target) => world.Mutations.Run(() =>
            (bool)Invoke(world.Work, "Assign", actor, target));

        internal static ActorBehaviour Spawn(ObjectSession world, string kind, float x) => world.Mutations.Run(() =>
            (ActorBehaviour)Invoke(world.Lifecycle, "SpawnActor", kind, x, true, ""));

        internal static void Damage(ObjectSession world, ICombatantCapability target, double amount) => world.Mutations.Run(() =>
        {
            Invoke(world.Combat, "Damage", target, amount, false);
            return true;
        });

        internal static void Shoot(ObjectSession world, float x, ICombatantCapability target, int damage) => world.Mutations.Run(() =>
        {
            Invoke(world.Projectiles, "Launch", new WorldPoint(x, world.Layout.GroundY - 15), target, damage);
            return true;
        });

        private static object Invoke(object owner, string method, params object[] args)
        {
            try { return owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
