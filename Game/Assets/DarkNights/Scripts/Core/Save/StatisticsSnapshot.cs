using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 本局累计击杀、居民损失和采集统计的存档值。它不持有实体或经济服务引用，可独立验证和比较。
    /// </summary>
    public sealed class StatisticsSnapshot
    {
        public int Kills { get; }
        public int Lost { get; }
        public ResourceAmounts Gathered { get; }

        public StatisticsSnapshot(
            int kills,
            int lost,
            ResourceAmounts gathered)
        {
            Kills = kills;
            Lost = lost;
            Gathered = gathered;
        }
    }
}
