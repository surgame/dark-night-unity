using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>矿床原型要求的 YYGC 实例能力；容量、钻机和阶段由同一个 MineralDepositBehaviour 持有。</summary>
    public interface IMineralDepositCapability : IEntityBehaviour, IArchetypeCapability
    {
    }
}
