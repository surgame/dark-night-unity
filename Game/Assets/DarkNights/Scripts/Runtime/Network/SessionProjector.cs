using System.Collections.Generic;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Session;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 在权威线程将一套活动模拟提供的冻结世界包装成完整会话帧，不保存实体或重新结算规则。
    /// 发布序号只属于该网络会话；各模拟实现负责冻结自己的状态与个体身份。
    /// </summary>
    internal sealed class SessionProjector
    {
        private long publication;

        public SessionViewData Capture(SessionAuthority session, SessionWorld game, IReadOnlyList<PresentationEvent> events,
            IReadOnlyList<PresentationEvent> remnants)
        {
            return new SessionViewData(checked(++publication), session.Epoch, session.Revision, session.ServerTick,
                session.PolicyRevision, session.ControlMode == CampControlMode.HostOnly, session.PlayerCount,
                session.ReadyCount, session.Loading, game.Paused, game.Speed, game.Elapsed, game.CaptureView(), events, remnants);
        }

        public void Clear() { }
    }
}
