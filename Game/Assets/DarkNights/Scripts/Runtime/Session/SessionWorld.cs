using System;
using DarkNights.Core.Config;
using DarkNights.Core.Save;
using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 权威队列访问一套活动模拟的有限合同，提供业务操作和冻结结果，不暴露可写实体。
    /// 迁移期间仅在整局创建时选择实现；一个会话始终只有一个模拟，旧入口在 U5 删除。
    /// </summary>
    public abstract class SessionWorld : IDisposable
    {
        public abstract GameCatalog Catalog { get; }
        public abstract LevelLayout Layout { get; }
        public abstract SessionFeedback Feedback { get; }
        public abstract bool Paused { get; }
        public abstract int Speed { get; }
        public abstract double Elapsed { get; }
        public abstract bool ValidRequest(SessionRequest request);
        public abstract int Apply(SessionRequest request, out int entityId);
        public abstract void Advance(double seconds);
        public abstract SessionSnapshot CaptureWorld();
        public abstract WorldViewData CaptureView();
        public abstract SessionWorld Restore(string json);
        public abstract SessionWorld Restart();
        public virtual void Activate() { }
        public abstract void Dispose();
    }
}
