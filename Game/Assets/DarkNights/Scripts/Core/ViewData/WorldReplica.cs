using System;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 客户端及 Host 共用的完整副本应用器，只保留最新一帧；同会话旧发布、旧 epoch 和回退版本均拒绝。
    /// 可信连接适配每次绑定调用 BeginConnection，异步回调必须携带返回的本地代次，防止上一连接污染新会话。
    /// 由展示线程串行调用；不会据此签发服务端 Ready，也不执行任何玩法。
    /// </summary>
    public sealed class WorldReplica
    {
        private long generation;
        private bool connected;
        public SessionViewData Current { get; private set; }

        public long BeginConnection()
        {
            generation = checked(generation + 1);
            connected = true;
            Current = null;
            return generation;
        }

        public void EndConnection(long connectionGeneration)
        {
            if (!connected || generation != connectionGeneration) return;
            connected = false;
            Current = null;
        }

        public bool Apply(long connectionGeneration, SessionViewData frame)
        {
            if (!CanApply(connectionGeneration, frame)) return false;
            Current = frame;
            return true;
        }

        public bool CanApply(long connectionGeneration, SessionViewData frame)
        {
            if (!connected || connectionGeneration != generation || frame == null) return false;
            if (Current != null)
            {
                if (frame.Publication <= Current.Publication || frame.Epoch < Current.Epoch ||
                    frame.ServerTick < Current.ServerTick || frame.PolicyRevision < Current.PolicyRevision) return false;
                if (frame.Epoch == Current.Epoch && (frame.Revision < Current.Revision || frame.Elapsed < Current.Elapsed)) return false;
            }
            return true;
        }

        // 回执只结束待确认反馈；需实际应用同 epoch 且不早于回执的世界才能操作相关实体视图。
        public bool HasApplied(int epoch, int revision) => revision >= 0 && connected && Current != null &&
            Current.Epoch == epoch && Current.Revision >= revision;
    }
}
