namespace DarkNights.Core.ViewData
{
    /// <summary>Journey唯一拥有的航程阶段；货物与信用点仍归各所属状态，过场表现不得自行推进或结算。</summary>
    public enum JourneyPhase
    {
        Orbit,
        Preparing,
        Transit,
        ArrivalSync,
        Descent,
        Landed,
        Gathering,
        Ascent
    }
}
