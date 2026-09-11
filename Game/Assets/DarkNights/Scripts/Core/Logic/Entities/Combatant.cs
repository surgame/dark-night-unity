using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Core.Logic.Entities
{
    /// <summary>
    /// 单位和建筑的权威生命边界，伤害、施工与修缮服务写入；工作点没有生命，也不参与战斗结算。
    /// </summary>
    public abstract class Combatant : Entity
    {
        public double Hp { get; internal set; }
        public double HitFlash { get; internal set; }
        public abstract double MaximumHp { get; }

        protected Combatant(int id, string kind, float x) : base(id, kind, x) { }
    }
}
