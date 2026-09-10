using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 建筑共享的只读数值与说明。伤害范围为独立快照；施工、受伤和训练队列由权威实例维护，定义不持有表现资源。
    /// </summary>
    public sealed class BuildingDefinition
    {
        public string Name { get; }
        public string Description { get; }
        public double Hp { get; }
        public float Width { get; }
        public double BuildSeconds { get; }
        public int Capacity { get; }
        public ResourceAmounts Cost { get; }
        public double Range { get; }
        public IReadOnlyList<int> Damage { get; }
        public double AttackSeconds { get; }

        public BuildingDefinition(
            string name,
            string description,
            double hp,
            float width,
            double buildSeconds,
            int capacity,
            ResourceAmounts cost,
            double range,
            IReadOnlyList<int> damage,
            double attackSeconds)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Hp = hp;
            Width = width;
            BuildSeconds = buildSeconds;
            Capacity = capacity;
            Cost = cost ?? throw new ArgumentNullException(nameof(cost));
            Range = range;
            Damage = new List<int>(damage ?? throw new ArgumentNullException(nameof(damage))).AsReadOnly();
            AttackSeconds = attackSeconds;
        }
    }
}
