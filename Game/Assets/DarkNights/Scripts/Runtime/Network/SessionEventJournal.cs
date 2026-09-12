using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Session;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 在权威线程订阅现有反馈出口，保存有界事件窗口，使相邻完整投影之间的短暂通知可被客户端去重消费。
    /// 事件不改变模拟、不进入存档；每次换世界重建，解绑旧反馈，溢出仅淘汰最旧表现记录。
    /// </summary>
    public sealed class SessionEventJournal : IDisposable
    {
        private readonly SessionFeedback feedback;
        private readonly Func<long> clock;
        private readonly Queue<PresentationEvent> events = new Queue<PresentationEvent>();
        private readonly List<PresentationEvent> remnants = new List<PresentationEvent>();
        private long sequence;

        public SessionEventJournal(SessionFeedback source, Func<long> tick)
        {
            feedback = source; clock = tick;
            feedback.Message += Message;
            feedback.Banner += Banner;
            feedback.Sound += Sound;
            feedback.Effect += Effect;
        }

        private void Message(string text, bool warning) => Add("message", text, warning: warning);
        private void Banner(string title, string detail) => Add("banner", title, detail);
        private void Sound(string id, float volume) => Add("sound", id, volume: volume);
        private void Effect(VisualCue cue) => Add("effect", cue: cue);
        private void Add(string type, string text = "", string detail = "", float volume = -11, bool warning = false, VisualCue cue = null)
        {
            var item = new PresentationEvent(checked(++sequence), clock(), type, text, detail, volume, warning, cue);
            if (cue != null && (cue.Kind == "corpse" || cue.Kind == "rubble"))
            {
                Prune();
                remnants.Add(item);
            }
            events.Enqueue(item);
            while (events.Count > SessionViewData.MaximumEvents) events.Dequeue();
        }

        public IReadOnlyList<PresentationEvent> Freeze() => Array.AsReadOnly(events.ToArray());
        public IReadOnlyList<PresentationEvent> FreezeRemnants()
        {
            Prune();
            if (remnants.Count > SessionViewData.MaximumRemnants)
                throw new InvalidOperationException("Active remnants exceed the supported projection limit; never silently truncate.");
            return Array.AsReadOnly(remnants.ToArray());
        }
        private void Prune() => remnants.RemoveAll(item => (clock() - item.Tick) / 60.0 >= PresentationCursor.Lifetime(item));
        public void Dispose()
        {
            feedback.Message -= Message;
            feedback.Banner -= Banner;
            feedback.Sound -= Sound;
            feedback.Effect -= Effect;
            events.Clear();
            remnants.Clear();
        }
    }
}
