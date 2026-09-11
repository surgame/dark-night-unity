using System;
using System.Collections.Generic;
using System.Linq;
namespace DarkNights.Core.Logic.State
{
    /// <summary>
    /// 夜袭导演的两个阶段。夜间必须完成生成并清空敌人，才能推进下一天。
    /// </summary>
    public enum WavePhase
    {
        Day, Night
    }
}
