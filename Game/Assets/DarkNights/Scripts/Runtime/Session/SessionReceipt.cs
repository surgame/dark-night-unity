namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 单条请求的冻结回执，携带发起连接代次及执行点的世界版本，供服务端定向返回。
    /// 不暴露可写世界；新实体只有在对应 revision 的展示副本到达后才能被客户端选择。
    /// </summary>
    public sealed class SessionReceipt
    {
        public int PlayerSlot { get; }
        public int ConnectionGeneration { get; }
        public long Sequence { get; }
        public int RequestEpoch { get; }
        public int Epoch { get; }
        public int Revision { get; }
        public int PolicyRevision { get; }
        public long ServerTick { get; }
        public SessionResultCode Code { get; }
        public int AffectedCount { get; }
        public int EntityId { get; }

        internal SessionReceipt(SessionConnection connection, SessionRequest request, int epoch, int revision,
            int policyRevision, long tick, SessionResultCode code, int affected = 0, int entityId = 0)
        {
            PlayerSlot = connection?.PlayerSlot ?? -1;
            ConnectionGeneration = connection?.Generation ?? 0;
            Sequence = request?.Sequence ?? 0;
            RequestEpoch = request?.Epoch ?? 0;
            Epoch = epoch;
            Revision = revision;
            PolicyRevision = policyRevision;
            ServerTick = tick;
            Code = code;
            AffectedCount = affected;
            EntityId = entityId;
        }
    }
}
