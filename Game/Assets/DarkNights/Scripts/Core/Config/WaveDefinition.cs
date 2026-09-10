using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 一夜的准备时间、刷怪顺序与清场补给。构造时复制敌人序列，模拟只维护当前索引，不改写共享配置。
    /// </summary>
    public sealed class WaveDefinition
    {
        public double DaySeconds { get; }
        public double SpawnInterval { get; }
        public IReadOnlyList<string> Enemies { get; }
        public ResourceAmounts Reward { get; }

        public WaveDefinition(
            double daySeconds,
            double spawnInterval,
            IReadOnlyList<string> enemies,
            ResourceAmounts reward)
        {
            DaySeconds = daySeconds;
            SpawnInterval = spawnInterval;
            Enemies = new List<string>(enemies ?? throw new ArgumentNullException(nameof(enemies))).AsReadOnly();
            Reward = reward ?? throw new ArgumentNullException(nameof(reward));
        }
    }
}
