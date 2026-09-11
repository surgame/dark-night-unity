using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 单次表现通知的有界传输字段；不携带可写实体或来源连接，只有权威投影生成此类型。
    /// 接收先验证类型、内容和字符串长度，再冻结；序号配合 epoch 防止可靠重发重复播放。
    /// </summary>
    [MemoryPackable]
    public partial class PresentationWire
    {
        public long Sequence { get; set; }
        public long Tick { get; set; }
        public string Type { get; set; }
        public string Text { get; set; }
        public string Detail { get; set; }
        public float Volume { get; set; }
        public bool Warning { get; set; }
        public string Kind { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public string ContentId { get; set; }
        public float Face { get; set; }
        public bool Enemy { get; set; }

        public static PresentationWire From(PresentationEvent value) => new PresentationWire
        {
            Sequence = value.Sequence, Tick = value.Tick, Type = value.Type,
            Text = value.Cue?.Text ?? value.Text, Detail = value.Detail, Volume = value.Volume, Warning = value.Warning,
            Kind = value.Cue?.Kind ?? "", X = value.Cue?.X ?? 0, Y = value.Cue?.Y ?? 0,
            ContentId = value.Cue?.ContentId ?? "", Face = value.Cue?.Face ?? 1, Enemy = value.Cue?.Enemy ?? false
        };

        public PresentationEvent Freeze() => new PresentationEvent(Sequence, Tick, Type, Type == "effect" ? "" : Text,
            Detail, Volume, Warning, Type == "effect" ? new VisualCue(Kind, X, Y, Text, ContentId, Face, Enemy) : null);
    }
}
