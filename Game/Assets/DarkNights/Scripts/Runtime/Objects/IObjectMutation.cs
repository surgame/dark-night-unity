using GameCore.Objects.NetworkStates;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 同步事务中一个状态所有者的提交边界；实现仅保留本次调用内的临时草稿。
    /// 完成或取消时释放草稿，不能把它作为另一套持续运行的世界。
    /// </summary>
    internal interface IObjectMutation
    {
        SessionStateChange PrepareCommit();
        void Complete();
    }
}
