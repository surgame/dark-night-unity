using System;
using System.Collections.Generic;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 客户端两帧表现插值，只引用冻结演员副本并插值位置与连续动作时间。
    /// 不外推、生成动作或修改生命；暂停及换 epoch 立即使用新帧，缺帧时停在最后权威位置。
    /// </summary>
    public sealed class PresentationTimeline
    {
        private SessionViewData current;
        private readonly Dictionary<int, ActorViewData> previous = new Dictionary<int, ActorViewData>();
        private double received, duration;

        public void Push(SessionViewData frame, double now)
        {
            if (frame == null) { Reset(); return; }
            if (current != null && frame.Publication <= current.Publication && frame.Epoch == current.Epoch) return;
            previous.Clear();
            duration = 0;
            if (current != null && current.Epoch == frame.Epoch && !frame.Paused && !frame.Loading &&
                frame.World.Camp.Mode == "Playing" && frame.ServerTick > current.ServerTick)
            {
                duration = Math.Min(0.2, (frame.ServerTick - current.ServerTick) / 60.0);
                foreach (ActorViewData actor in current.World.Actors) previous.Add(actor.Id, actor);
            }
            current = frame; received = now;
        }

        public float X(ActorViewData actor, double now) => Previous(actor, out var old)
            ? old.X + (actor.X - old.X) * (float)Ratio(now) : actor.X;

        public double ActionTime(ActorViewData actor, double now)
        {
            if (!Previous(actor, out var old) || old.Activity != actor.Activity || actor.ActionTime < old.ActionTime)
                return actor.ActionTime;
            return old.ActionTime + (actor.ActionTime - old.ActionTime) * Ratio(now);
        }

        public void Reset() { current = null; previous.Clear(); duration = 0; }
        private bool Previous(ActorViewData actor, out ActorViewData value) =>
            previous.TryGetValue(actor.Id, out value) && value.Kind == actor.Kind;
        private double Ratio(double now) => duration <= 0 ? 1 : Math.Max(0, Math.Min(1, (now - received) / duration));
    }
}
