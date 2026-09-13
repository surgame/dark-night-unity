using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>单位原型要求的真实移动能力；由唯一会话调度调用，不自行注册逐帧玩法更新。</summary>
    public interface IMovementCapability : IArchetypeCapability
    {
        bool MoveTo(float x, double delta);
    }
}
