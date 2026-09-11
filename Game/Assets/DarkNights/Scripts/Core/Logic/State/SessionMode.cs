using System;
using System.Collections.Generic;
using System.Linq;
namespace DarkNights.Core.Logic.State
{
    /// <summary>
    /// 一局游戏的生命周期阶段。只有Playing允许推进模拟，胜负状态由会话统一切换。
    /// </summary>
    public enum SessionMode
    {
        Menu, Playing, Won, Lost
    }
}
