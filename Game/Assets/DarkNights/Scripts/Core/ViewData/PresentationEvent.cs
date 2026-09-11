namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 权威端一次消息、横幅、声音或视觉通知的冻结展示记录；序号只在当前 epoch 内有意义。
    /// Tick 使用未缩放服务时钟，供暂停期间的表现寿命和晚加入过滤，不参与伤害、资源或恢复存档。
    /// </summary>
    public sealed class PresentationEvent
    {
        public long Sequence { get; }
        public long Tick { get; }
        public string Type { get; }
        public string Text { get; }
        public string Detail { get; }
        public float Volume { get; }
        public bool Warning { get; }
        public VisualCue Cue { get; }

        public PresentationEvent(long sequence, long tick, string type, string text = "", string detail = "",
            float volume = -11, bool warning = false, VisualCue cue = null)
        {
            Sequence = sequence; Tick = tick; Type = type; Text = text; Detail = detail;
            Volume = volume; Warning = warning; Cue = cue;
        }
    }
}
