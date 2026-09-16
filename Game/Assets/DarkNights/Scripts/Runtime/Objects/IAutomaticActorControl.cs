namespace DarkNights.Runtime.Objects
{
    /// <summary>可由定义装配替换的单位自动决策入口；只写所属 ActorState，不拥有额外实体状态。</summary>
    public interface IAutomaticActorControl : GameCore.Objects.Definition.IArchetypeCapability
    {
        void Tick(double delta);
    }
}
