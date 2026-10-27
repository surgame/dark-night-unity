using System;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Objects
{
    /// <summary>YYGC 会话对象的航程状态所有者；所有阶段修改属于同步事务，配置目录由同对象的流程能力冻结。</summary>
    public sealed partial class ExpeditionJourneyBehaviour : SessionStateBehaviour<ExpeditionJourneyState>
    {
        private NavigationSurveyData surveyCache;
        internal void Prepare()
        {
            PrepareState(new ExpeditionJourneyState
            {
                Enabled = Session.Flow.Enabled, Revision = 1, Phase = JourneyPhase.Orbit,
                ContentFingerprint = Session.Flow.ContentFingerprint,
                SurveyBits = NavigationSurveyData.Initial(Session.Flow.Navigation, 40, 30).Encoded
            });
        }

        public JourneyViewData Capture()
        {
            var s = Read();
            return s == null || !s.Enabled ? null : new JourneyViewData(s.Enabled, s.JourneyId, s.Revision,
                s.Phase, s.PlanetId, s.Seed, s.MapId, s.ContentFingerprint, s.PhaseElapsed, s.Error, Session.Flow.Planets,
                new JourneyNavigationData(Session.Flow.Navigation, s.StarX, s.StarY, s.RunId, s.SettledRunId, s.LastIncome,
                    Survey(), s.CoordinateFlight, s.NavigationTargetX, s.NavigationTargetY));
        }

        internal NavigationSurveyData Survey()
        {
            string encoded = Read().SurveyBits;
            if (surveyCache == null || surveyCache.Encoded != encoded) surveyCache = new NavigationSurveyData(encoded);
            return surveyCache;
        }

        internal void RevealTravel(float x, float y, float targetX, float targetY)
        {
            var revealed = Survey().Reveal(x, y, targetX, targetY);
            Edit().SurveyBits = revealed.Encoded;
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
            if (value.Navigation == null) throw new FormatException("航程缺少冻结导航资料。");
            var n = value.Navigation;
            s.StarX = n.X; s.StarY = n.Y; s.RunId = n.RunId;
            s.SettledRunId = n.SettledRunId; s.LastIncome = n.LastIncome;
            s.SurveyBits = n.Survey.Encoded; s.CoordinateFlight = n.CoordinateFlight;
            s.NavigationTargetX = n.TargetX; s.NavigationTargetY = n.TargetY;
        }

        internal void SetPhase(JourneyPhase phase, string error = "")
        {
            var s = Edit(); s.Phase = phase; s.PhaseElapsed = 0;
            s.Revision = checked(s.Revision + 1); s.Error = error.Length <= 512 ? error : error.Substring(0, 512);
        }
    }
}
