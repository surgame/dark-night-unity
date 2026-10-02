using System;

namespace DarkNights.Core.Config
{
    /// <summary>采集能力支持的目标类别；只描述作用对象，不作为工具身份或状态所有者。</summary>
    [Flags]
    public enum MiningTargetKinds
    {
        None = 0,
        Foreground = 1,
        MineralDeposit = 2
    }
}
