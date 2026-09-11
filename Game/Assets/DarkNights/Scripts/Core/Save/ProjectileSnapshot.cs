using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 飞行中伤害的存档记录，坐标沿用二元素数组以兼容schema v1。不存在的历史目标ID允许保留，抵达后安全消耗。
    /// </summary>
    public sealed class ProjectileSnapshot
    {
        public IReadOnlyList<double> From { get; }
        public IReadOnlyList<double> To { get; }
        public int TargetId { get; }
        public int Damage { get; }
        public double Age { get; }
        public double Duration { get; }

        public ProjectileSnapshot(
            IReadOnlyList<double> from,
            IReadOnlyList<double> to,
            int targetId,
            int damage,
            double age,
            double duration)
        {
            From = from == null ? null : new List<double>(from).AsReadOnly();
            To = to == null ? null : new List<double>(to).AsReadOnly();
            TargetId = targetId;
            Damage = damage;
            Age = age;
            Duration = duration;
        }
    }
}
