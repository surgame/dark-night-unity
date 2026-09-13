using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>单位原型要求的实例状态能力；只有实际 YYGC 业务 Behaviour 可以满足该装配合同。</summary>
    public interface IActorCapability : IEntityBehaviour, IArchetypeCapability
    {
    }
}
