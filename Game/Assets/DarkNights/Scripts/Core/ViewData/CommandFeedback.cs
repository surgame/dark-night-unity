namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 定向回执的不可变展示结果；用于结束待确认反馈，不能据此修改库存或创建世界实体。
    /// Ready 回执与业务结果显式区分，连接与 epoch 由接收适配过滤后再发布。
    /// </summary>
    public sealed class CommandFeedback
    {
        public long Sequence { get; }
        public int Epoch { get; }
        public int Revision { get; }
        public string Code { get; }
        public int AffectedCount { get; }
        public int EntityId { get; }
        public bool ReadyReply { get; }
        public int PlayerSlot { get; }
        public int ConnectionGeneration { get; }

        public CommandFeedback(long sequence, int epoch, int revision, string code, int affectedCount,
            int entityId, bool readyReply, int playerSlot, int connectionGeneration)
        {
            Sequence = sequence;
            Epoch = epoch;
            Revision = revision;
            Code = code;
            AffectedCount = affectedCount;
            EntityId = entityId;
            ReadyReply = readyReply;
            PlayerSlot = playerSlot;
            ConnectionGeneration = connectionGeneration;
        }
    }
}
