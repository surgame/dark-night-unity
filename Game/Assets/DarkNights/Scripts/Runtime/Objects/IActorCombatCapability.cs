using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 单位原型必须装配的索敌与攻击能力；没有独立时钟或生命副本。
    /// 只由所属单位在会话事务的单位阶段调用，客户端不执行这些入口。
    /// </summary>
    public interface IActorCombatCapability : IArchetypeCapability
    {
        void FindTarget();
        void TickAttack(double delta);
    }
}
