using System;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 启动阶段一次装配的只读数值与关卡目录，不访问文件、引擎或序列化库。
    /// 检查版本和跨定义引用后供各局共享；不创建布局、随机数或可写世界状态。
    /// </summary>
    public sealed class GameCatalog
    {
        public BalanceDefinition Balance { get; }
        public LevelDefinition Level { get; }

        public GameCatalog(BalanceDefinition balance, LevelDefinition level)
        {
            Balance = balance ?? throw new ArgumentNullException(nameof(balance));
            Level = level ?? throw new ArgumentNullException(nameof(level));
            if (balance.SchemaVersion != 1 || level.Waves.Count == 0 || string.IsNullOrWhiteSpace(level.Id))
                throw new ArgumentException("Unsupported balance version or empty level.");
            if (balance.Units.Count == 0 || balance.Buildings.Count == 0 || balance.Worksites.Count == 0)
                throw new ArgumentException("Content dictionaries must not be empty.");
            foreach (WaveDefinition wave in level.Waves)
            {
                if (wave == null || wave.Enemies.Count == 0)
                    throw new ArgumentException("Each wave must contain enemies.");
                foreach (string enemy in wave.Enemies)
                    if (enemy == null || !balance.Units.ContainsKey(enemy))
                        throw new ArgumentException("Unknown enemy in wave: " + enemy);
            }
        }
    }
}
