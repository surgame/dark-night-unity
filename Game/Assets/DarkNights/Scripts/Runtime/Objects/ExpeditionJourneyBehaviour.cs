using System;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Objects
{
    /// <summary>YYGC 会话对象的航程状态所有者；所有阶段修改属于同步事务，配置目录由同对象的流程能力冻结。</summary>
    public sealed partial class ExpeditionJourneyBehaviour : SessionStateBehaviour<ExpeditionJourneyState>
    {
        internal void Prepare()
        {
            PrepareState(new ExpeditionJourneyState
            {
                Enabled = Session.Flow.Enabled, Revision = 1, Phase = JourneyPhase.Orbit,
                ContentFingerprint = Session.Flow.ContentFingerprint
            });
        }

        public JourneyViewData Capture()
        {
            var s = Read();
            return s == null || !s.Enabled ? null : new JourneyViewData(s.Enabled, s.JourneyId, s.Revision,
                s.Phase, s.PlanetId, s.Seed, s.MapId, s.ContentFingerprint, s.PhaseElapsed, s.Error, Session.Flow.Planets);
        }

        internal void Restore(JourneyViewData value)
        {
            if (!Session.Flow.Accepts(value)) throw new FormatException("存档星球配置与当前会话不一致。");
            var s = Edit();
            if (value == null)
            {
                s.Enabled = false; s.JourneyId = s.PlanetId = s.Seed = s.MapId = s.ContentFingerprint = s.Error = "";
                s.Revision = 1; s.Phase = JourneyPhase.Orbit; s.PhaseElapsed = 0; return;
            }
            s.Enabled = value.Enabled; s.JourneyId = value.JourneyId; s.Revision = value.Revision;
            s.Phase = value.Phase; s.PlanetId = value.PlanetId; s.Seed = value.Seed; s.MapId = value.MapId;
            s.ContentFingerprint = value.ContentFingerprint; s.PhaseElapsed = value.PhaseElapsed; s.Error = value.Error;
        }

        internal void SetPhase(JourneyPhase phase, string error = "")
        {
            var s = Edit(); s.Phase = phase; s.PhaseElapsed = 0;
            s.Revision = checked(s.Revision + 1); s.Error = error.Length <= 512 ? error : error.Substring(0, 512);
        }
    }
}
