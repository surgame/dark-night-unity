using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>单位原型要求的统一移动能力；MoveTo只提交本步意图，ActorBehaviour随后调用一次Tick执行。</summary>
    public interface IMovementCapability : IArchetypeCapability
    {
        bool MoveTo(float x, double delta);
        void Tick(double delta);
    }
}
