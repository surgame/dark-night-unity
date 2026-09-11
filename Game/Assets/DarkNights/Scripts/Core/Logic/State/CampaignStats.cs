using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;

namespace DarkNights.Core.Logic.State
{
    /// <summary>
    /// 本局击杀、损失与累计采集统计。库存另由经济服务拥有，累计采集不会因消费而减少。
    /// </summary>
    public sealed class CampaignStats
    {
        public int Kills { get; internal set; }
        public int Lost { get; internal set; }
        public ResourceAmounts Gathered { get; internal set; } = new();

        internal void Reset()
        {
            Kills = 0;
            Lost = 0;
            Gathered = new();
        }
    }
}
