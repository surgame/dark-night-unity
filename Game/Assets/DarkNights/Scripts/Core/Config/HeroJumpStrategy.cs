namespace DarkNights.Core.Config
{
    /// <summary>普通跳跃的按键策略；由冻结规则选择，坡面碰撞和喷气阶段共用同一权威运动。工作台可独立预览。</summary>
    public enum HeroJumpStrategy
    {
        Fixed = 0,
        HoldHeight = 1
    }
}
