using DarkNights.Core.Config;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 库存、人口、波次、胜负和累计统计的冻结展示值；只含 HUD 所需数据，不包含随机数和存档恢复游标。
    /// </summary>
    public sealed class CampViewData
    {
        public ResourceAmounts Stock { get; }
        public int Population { get; }
        public int Capacity { get; }
        public double RecruitCooldown { get; }
        public int WaveIndex { get; }
        public string WavePhase { get; }
        public double DayRemaining { get; }
        public int EnemyCount { get; }
        public string Mode { get; }
        public int Kills { get; }
        public int Lost { get; }
        public ResourceAmounts Gathered { get; }
        public int NextSpawn { get; }

        public CampViewData(
            ResourceAmounts stock,
            int population,
            int capacity,
            double recruitCooldown,
            int waveIndex,
            string wavePhase,
            double dayRemaining,
            int enemyCount,
            string mode,
            int kills,
            int lost,
            ResourceAmounts gathered,
            int nextSpawn = 0)
        {
            Stock = stock;
            Population = population;
            Capacity = capacity;
            RecruitCooldown = recruitCooldown;
            WaveIndex = waveIndex;
            WavePhase = wavePhase;
            DayRemaining = dayRemaining;
            EnemyCount = enemyCount;
            Mode = mode;
            Kills = kills;
            Lost = lost;
            Gathered = gathered;
            NextSpawn = nextSpawn;
        }
    }
}
