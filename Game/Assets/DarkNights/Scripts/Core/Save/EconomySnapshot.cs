using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 库存和食物/招募计时的传输记录。恢复直接替换运行状态，不再次扣除或发放资源。
    /// </summary>
    public sealed class EconomySnapshot
    {
        public ResourceAmounts Resources { get; }
        public double UpkeepElapsed { get; }
        public double StarvationElapsed { get; }
        public double RecruitCooldown { get; }

        public EconomySnapshot(
            ResourceAmounts resources,
            double upkeepElapsed,
            double starvationElapsed,
            double recruitCooldown)
        {
            Resources = resources;
            UpkeepElapsed = upkeepElapsed;
            StarvationElapsed = starvationElapsed;
            RecruitCooldown = recruitCooldown;
        }
    }
}
