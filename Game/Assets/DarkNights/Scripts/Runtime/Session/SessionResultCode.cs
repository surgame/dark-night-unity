namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 服务端接收与执行的明确结果。Pending 仅表示进入有界队列；Applied 表示已执行操作，
    /// 对 BeginLoad 则仅表示获得加载票据，不能据此显示文件加载成功。
    /// </summary>
    public enum SessionResultCode
    {
        Pending, Applied, NoEffect, InvalidRequest, InvalidConnection, ProtocolMismatch,
        EpochChanged, NotReady, PolicyChanged, PermissionDenied, SequenceExpired,
        SequenceConflict, QueueFull, Loading, SessionClosed, ObjectUnavailable
    }
}
