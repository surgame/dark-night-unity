using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;

namespace DarkNights.Core.Logic.Entities
{
    /// <summary>
    /// 一座建筑的生命、施工状态、占用者和训练队列。实例只推进所属的建造/训练/塔攻击，完工与死亡时通过会话服务统一处理关系。
    /// </summary>
    public sealed class Building : Combatant
    {
        private readonly GameSession _session;
        public BuildingDefinition Definition { get; }
        public override double MaximumHp => Definition.Hp;
        public double Progress { get; internal set; }
        public int WorkerId { get; internal set; }
        public int FarmSiteId { get; internal set; }
        public double AttackClock { get; internal set; }
        public List<TrainingOrder> TrainingQueue { get; } = new List<TrainingOrder>();
        public bool IsComplete => Progress >= 1 && Hp > 0;

        internal Building(GameSession session, int id, string kind, float x, bool complete) : base(id, kind, x)
        {
            _session = session;
            Definition = session.Catalog.Balance.Buildings[kind];
            Progress = complete ? 1 : 0;
            Hp = Definition.Hp * (complete ? 1 : 0.2);
        }

        internal void Tick(double delta)
        {
            if (Hp <= 0)
                return;
            HitFlash = Math.Max(0, HitFlash - delta);
            AttackClock = Math.Max(0, AttackClock - delta);
            if (!IsComplete)
            {
                _session.Construction.Advance(this, delta);
                return;
            }
            if (Kind == "barracks")
                _session.Training.Advance(this, delta);
            else if (Kind == "tower" && AttackClock <= 0)
            {
                var target = _session.Combat.NearestEnemy(X, Definition.Range);
                if (target == null)
                    return;
                AttackClock = Definition.AttackSeconds;
                int damage = _session.Random.RandiRange(Definition.Damage[0], Definition.Damage[1]);
                _session.Projectiles.Launch(new(X, _session.GroundY - 38), target, damage);
            }
        }
    }
}
