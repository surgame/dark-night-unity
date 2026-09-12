using DarkNights.Core.Save;
using DarkNights.Runtime.Session;

namespace DarkNights.Runtime.Save
{
    /// <summary>
    /// 唯一权威线程签发的一次存储工作，快照在命令执行边界冻结，票据不能由网络字段重建。
    /// 保存任务只读快照；读取结果必须回到原会话线程，通过同一个加载票据提交。
    /// </summary>
    public sealed class SessionStorageRequest
    {
        public SessionOperation Operation { get; }
        public int Slot { get; }
        public SessionReceipt Ticket { get; }
        public SessionSnapshot Snapshot { get; }

        internal SessionStorageRequest(SessionOperation operation, int slot, SessionReceipt ticket, SessionSnapshot snapshot)
        {
            Operation = operation;
            Slot = slot;
            Ticket = ticket;
            Snapshot = snapshot;
        }
    }
}
