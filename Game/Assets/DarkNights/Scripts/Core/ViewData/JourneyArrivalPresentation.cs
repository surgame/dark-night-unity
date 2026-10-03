using System;

namespace DarkNights.Core.ViewData
{
    /// <summary>本地航程表现时钟；地图未绘制时保留太空，正常到达后逐步显露地表，不推进权威阶段或 Ready。</summary>
    public sealed class JourneyArrivalPresentation
    {
        public const double RevealSeconds = 1.2;
        private SessionViewData previous;
        private double progress = 1;
        private bool arriving;
        public double StarTravel { get; private set; }
        public double StarSpeed { get; private set; } = 1.5;
        public float SurfaceAmount => (float)(progress * progress * (3 - 2 * progress));
        public bool Complete => !arriving || progress >= 1;

        public bool Observe(SessionViewData frame)
        {
            bool arrival = JourneyContinuity.IsArrival(previous, frame);
            var journey = frame?.World.Expedition?.Journey;
            bool space = journey?.Enabled == true && journey.Phase is JourneyPhase.Orbit or JourneyPhase.Preparing or JourneyPhase.Transit;
            if (arrival) { arriving = true; progress = 0; }
            else if (previous == null || frame == null || previous.Epoch != frame.Epoch || journey?.Enabled != true || space)
            {
                arriving = false; progress = space ? 0 : 1;
                if (frame == null) { StarTravel = 0; StarSpeed = 1.5; }
            }
            previous = frame;
            return arrival;
        }

        public void Advance(double seconds, bool destinationDrawn, bool paused, double targetStarSpeed)
        {
            if (paused || seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            if (arriving && destinationDrawn) progress = Math.Min(1, progress + seconds / RevealSeconds);
            double step = 140 * seconds;
            StarSpeed += Math.Max(-step, Math.Min(step, targetStarSpeed - StarSpeed));
            StarTravel += StarSpeed * seconds / 1000;
        }
    }
}
