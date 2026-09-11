using System;
using System.Collections.Generic;
using System.Linq;
namespace DarkNights.Core.Logic.State
{
    /// <summary>
    /// 单位命令状态，区分旅行、到达后的工作和训练排队。存档使用对应snake_case名称，不能随意改名。
    /// </summary>
    public enum ActorActivity
    {
        Idle, Move, WorkMove, Work, BuildMove, Build, TrainingMove, Training, Attack
    }
}
