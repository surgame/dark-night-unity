using System;
using System.Collections.Generic;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 客户端表现事件游标，仅记录已消费序号；重复投影不会重复播音或创建效果。
    /// epoch 切换清空游标，重连由调用者 Reset；不保存游戏状态，不补播已过期通知。
    /// </summary>
    public sealed class PresentationCursor
    {
        private int epoch;
        private long sequence;
        private readonly HashSet<long> remnants = new HashSet<long>();

        public IReadOnlyList<PresentationEvent> Consume(SessionViewData frame)
        {
            if (frame == null) return Array.Empty<PresentationEvent>();
            if (frame.Epoch != epoch) { epoch = frame.Epoch; sequence = 0; remnants.Clear(); }
            var active = new HashSet<long>();
            foreach (PresentationEvent item in frame.Remnants) active.Add(item.Sequence);
            remnants.RemoveWhere(id => !active.Contains(id));
            var result = new List<PresentationEvent>();
            foreach (PresentationEvent item in frame.Events)
            {
                if (item.Sequence <= sequence) continue;
                sequence = item.Sequence;
                if (Age(frame, item) >= Lifetime(item)) continue;
                if (active.Contains(item.Sequence) && !remnants.Add(item.Sequence)) continue;
                result.Add(item);
            }
            foreach (PresentationEvent item in frame.Remnants)
                if (Age(frame, item) < Lifetime(item) && remnants.Add(item.Sequence)) result.Add(item);
            return result.AsReadOnly();
        }

        public void Reset() { epoch = 0; sequence = 0; remnants.Clear(); }
        public static double Age(SessionViewData frame, PresentationEvent item) => (frame.ServerTick - item.Tick) / 60.0;
        public static double Lifetime(PresentationEvent item) => item.Type == "sound" ? 0.5 :
            item.Type != "effect" ? 5 : item.Cue.Kind == "corpse" ? 8 : item.Cue.Kind == "rubble" ? 180 :
            item.Cue.Kind == "command" ? 0.8 : 1.6;
    }
}
