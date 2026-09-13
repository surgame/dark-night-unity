namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 可受伤对象的只读生命合同；实际生命仍由所属家族 State 唯一拥有。
    /// 索敌和箭矢以 EntityId 查此能力，工位不能通过该合同参与伤害结算。
    /// </summary>
    public interface ICombatantCapability : IEntityBehaviour
    {
        double Hp { get; }
        double MaximumHp { get; }
    }
}
