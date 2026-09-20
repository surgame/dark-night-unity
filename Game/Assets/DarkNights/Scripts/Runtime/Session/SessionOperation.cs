namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 权威会话当前支持的业务意图；枚举只描述操作，不授予权限，也不是已注册的网络 wire 类型。
    /// BeginLoad 只锁定切世界边界，文件读取与完成通知由服务端存储适配负责。
    /// </summary>
    public enum SessionOperation
    {
        IssueOrders, PlaceBuilding, TrainActors, Recruit, Repair,
        SetPaused, SetSpeed, StartNight, SetControlMode, BeginLoad, Save, Restart,
        ClaimHero, ReleaseHero, SelectHeroItem, UseHeroItem, Expedition
    }
}
