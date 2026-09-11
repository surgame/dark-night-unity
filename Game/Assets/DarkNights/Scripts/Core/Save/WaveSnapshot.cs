using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 当前夜次及刷怪游标的schema v1记录。恢复不会重新生成已经出现的敌人，也不会再次发放已领取的补给。
    /// </summary>
    public sealed class WaveSnapshot
    {
        public int Index { get; }
        public WavePhase Phase { get; }
        public double DayRemaining { get; }
        public double SpawnElapsed { get; }
        public int NextSpawn { get; }

        public WaveSnapshot(
            int index,
            WavePhase phase,
            double dayRemaining,
            double spawnElapsed,
            int nextSpawn)
        {
            Index = index;
            Phase = phase;
            DayRemaining = dayRemaining;
            SpawnElapsed = spawnElapsed;
            NextSpawn = nextSpawn;
        }
    }
}
