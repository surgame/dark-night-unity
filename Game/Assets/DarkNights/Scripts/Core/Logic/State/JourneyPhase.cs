namespace DarkNights.Core.Logic.State
{
    /// <summary>会话航程的权威阶段；地面探索结算仍由远征营地状态拥有，过场表现不得自行推进此阶段。</summary>
    public enum JourneyPhase
    {
        Orbit,
        Preparing,
        Transit,
        ArrivalSync,
        Descent,
        Landed
    }
}
