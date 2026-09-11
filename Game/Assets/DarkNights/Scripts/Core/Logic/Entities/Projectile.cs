using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Core.Logic.Entities
{
    /// <summary>
    /// 权威模拟拥有的在飞箭矢；目标和伤害在发射时固定，抵达先移除再结算一次，表现不能写入。
    /// </summary>
    public sealed class Projectile
    {
        public double Age { get; internal set; }
        public WorldPoint From { get; }
        public WorldPoint To { get; internal set; }
        public int TargetId { get; }
        public int Damage { get; }
        public double Duration { get; }

        public Projectile(WorldPoint From, WorldPoint To, int TargetId, int Damage, double Duration)
        {
            this.From = From;
            this.To = To;
            this.TargetId = TargetId;
            this.Damage = Damage;
            this.Duration = Duration;
        }
    }
}
