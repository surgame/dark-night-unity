namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 会话队列与结果窗口共用的一次请求记录，所有写入都在创建会话的线程发生。
    /// 执行后用新的冻结回执替换 Pending；请求与连接引用仅在有界队列及缓存内保留。
    /// </summary>
    internal sealed class SessionCommandEntry
    {
        public SessionConnection Connection { get; }
        public SessionRequest Request { get; }
        public SessionReceipt Receipt { get; set; }

        public SessionCommandEntry(SessionConnection connection, SessionRequest request, SessionReceipt receipt)
        {
            Connection = connection;
            Request = request;
            Receipt = receipt;
        }
    }
}
