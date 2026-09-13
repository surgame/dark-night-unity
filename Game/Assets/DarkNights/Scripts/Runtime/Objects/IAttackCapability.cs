using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 职业定义装配的命中方式，近战与箭矢实现共享同一前摇和随机伤害入口。
    /// 能力不保存状态，发射后的箭矢归会话拥有；一次命中只调用一个实现。
    /// </summary>
    public interface IAttackCapability : IArchetypeCapability
    {
        void Hit(ICombatantCapability target, int damage);
    }
}
