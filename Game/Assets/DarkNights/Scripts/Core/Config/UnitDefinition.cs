using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 职业或敌种共享的只读战斗参数。伤害范围在构造时复制，实例生命、攻击时钟和目标不写入此定义。
    /// </summary>
    public sealed class UnitDefinition
    {
        public string Name { get; }
        public double Hp { get; }
        public double Armor { get; }
        public IReadOnlyList<int> Damage { get; }
        public double Speed { get; }
        public double Range { get; }
        public double Aggro { get; }
        public double Leash { get; }
        public double AttackSeconds { get; }
        public double Windup { get; }
        public int Gold { get; }
        public ResourceAmounts Cost { get; }

        public UnitDefinition(
            string name,
            double hp,
            double armor,
            IReadOnlyList<int> damage,
            double speed,
            double range,
            double aggro,
            double leash,
            double attackSeconds,
            double windup,
            int gold,
            ResourceAmounts cost)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Hp = hp;
            Armor = armor;
            Damage = new List<int>(damage ?? throw new ArgumentNullException(nameof(damage))).AsReadOnly();
            Speed = speed;
            Range = range;
            Aggro = aggro;
            Leash = leash;
            AttackSeconds = attackSeconds;
            Windup = windup;
            Gold = gold;
            Cost = cost ?? throw new ArgumentNullException(nameof(cost));
        }
    }
}
