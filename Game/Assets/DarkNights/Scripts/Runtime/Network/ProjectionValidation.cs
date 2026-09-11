using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 在客户端创建只读副本前校验具体投影字段及有界集合；不重算资源、伤害或目标决策。
    /// 校验失败不能替换当前帧，也不发送 Ready。内容类别以握手匹配后的只读目录为准。
    /// </summary>
    internal static class ProjectionValidation
    {
        public static void Validate(SessionWire frame, GameCatalog catalog, LevelLayout layout)
        {
            WorldWire world = frame.World;
            Require(world != null && world.Camp != null && world.Actors != null && world.Buildings != null &&
                world.Worksites != null && world.Projectiles != null);
            Require((long)world.Actors.Length + world.Buildings.Length + world.Worksites.Length <= WorldViewData.MaximumEntities &&
                world.Projectiles.Length <= WorldViewData.MaximumProjectiles);
            var camp = world.Camp;
            Require(camp.Stock != null && camp.Gathered != null && camp.Stock.Freeze().IsValid() && camp.Gathered.Freeze().IsValid());
            Require(camp.Population >= 0 && camp.Capacity >= 0 && camp.EnemyCount >= 0 && camp.Kills >= 0 && camp.Lost >= 0 &&
                camp.WaveIndex >= 0 && camp.WaveIndex < catalog.Level.Waves.Count &&
                Enum.TryParse<WavePhase>(camp.WavePhase, out var phase) && Enum.IsDefined(typeof(WavePhase), phase) &&
                Enum.TryParse<SessionMode>(camp.Mode, out var mode) && Enum.IsDefined(typeof(SessionMode), mode));
            Nonnegative(camp.RecruitCooldown, camp.DayRemaining);
            int trainingCount = 0;
            foreach (var a in world.Actors)
            {
                Require(a != null && Text(a.Kind, 64) && Text(a.Name, 256) && catalog.Balance.Units.ContainsKey(a.Kind) &&
                    Enum.TryParse<ActorActivity>(a.Activity, out var activity) && Enum.IsDefined(typeof(ActorActivity), activity));
                Position(a.X, layout);
                Nonnegative(a.Hp, a.ActionTime, a.Windup, a.HitFlash);
                Require(a.Face == -1 || a.Face == 0 || a.Face == 1);
            }
            foreach (var b in world.Buildings)
            {
                Require(b != null && Text(b.Kind, 64) && catalog.Balance.Buildings.ContainsKey(b.Kind) && b.Training != null);
                trainingCount += b.Training.Length;
                Require(trainingCount <= WorldViewData.MaximumEntities);
                Position(b.X, layout);
                Nonnegative(b.Hp, b.Progress, b.HitFlash);
                Require(b.Progress <= 1);
                foreach (var t in b.Training)
                {
                    Require(t != null && Text(t.Kind, 64) && catalog.Balance.Units.ContainsKey(t.Kind) &&
                        world.Actors.Any(a => a.Id == t.ActorId));
                    Nonnegative(t.Remaining);
                }
            }
            foreach (var w in world.Worksites)
            {
                Require(w != null && Text(w.Kind, 64) && catalog.Balance.Worksites.ContainsKey(w.Kind) && w.Amount >= -1 && w.Variant >= 0);
                Position(w.X, layout);
                Nonnegative(w.Progress);
            }
            foreach (var p in world.Projectiles)
            {
                Require(p != null);
                Nonnegative(p.Age, p.Duration);
                Require(p.Duration > 0 && p.Age <= p.Duration);
                Require(Finite(p.FromX) && Finite(p.FromY) && Finite(p.ToX) && Finite(p.ToY));
            }
        }

        private static void Position(double x, LevelLayout layout) => Require(Finite(x) && x >= -100 && x <= layout.WorldWidth + 100);
        private static bool Text(string value, int maximum) => value != null && value.Length <= maximum;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static void Nonnegative(params double[] values)
        {
            foreach (double value in values) Require(Finite(value) && value >= 0);
        }
        private static void Require(bool valid)
        {
            if (!valid) throw new FormatException("Invalid complete world projection.");
        }
    }
}
