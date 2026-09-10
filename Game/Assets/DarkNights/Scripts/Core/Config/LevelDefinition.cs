using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 关卡 JSON 的只读身份、随机种子与波次配置。只持有冻结的内容数据；场景布局将从唯一可编辑场景来源另行提供，不能用缺省零坐标生成世界。
    /// </summary>
    public sealed class LevelDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public ulong Seed { get; }
        public IReadOnlyList<WaveDefinition> Waves { get; }

        public LevelDefinition(
            string id,
            string name,
            ulong seed,
            IReadOnlyList<WaveDefinition> waves)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Seed = seed;
            Waves = new List<WaveDefinition>(waves ?? throw new ArgumentNullException(nameof(waves))).AsReadOnly();
        }
    }
}
