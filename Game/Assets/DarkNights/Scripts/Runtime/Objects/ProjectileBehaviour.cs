using System;
using System.Linq;
using DarkNights.Core.Logic;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 会话箭矢的唯一状态所有者，单位和塔只请求发射，飞行阶段按插入顺序结算。
    /// 目标死亡后仍完成轨迹；抵达先移除再伤害，保存复制全部未命中的记录。
    /// </summary>
    [RequireConfig(typeof(HandheldConfig))]
    public sealed partial class ProjectileBehaviour : SessionStateBehaviour<ProjectileState>
    {
        [Inject] private HandheldConfig config;
        internal HandheldConfig Settings => config;
        internal void Prepare()
        {
            config.Validate();
            PrepareState(new ProjectileState());
        }

        internal bool LaunchHandheld(ActorState actor, bool bomb, float charge)
        {
            ProjectileState state = Edit();
            int slot = Array.FindIndex(state.Ballistics, p => p.Kind == 0);
            if (slot < 0 || state.Shots.Length + state.Ballistics.Count(p => p.Kind != 0) >= 1024) return false;
            float radians = actor.AimAngle * (float)Math.PI / 180;
            float dx = (float)Math.Cos(radians), dy = (float)Math.Sin(radians);
            float x = actor.X, h = actor.Height + config.HandHeight;
            float radius = bomb ? config.BombRadius : config.BulletRadius;
            // 从手部扫到枪口，紧贴墙面时不得把出生点放到墙的另一侧。
            for (float distance = 1; distance <= config.MuzzleDistance; distance++)
            {
                float nx = actor.X + dx * distance, nh = actor.Height + config.HandHeight + dy * distance;
                if (BallisticMotion.Solid(Session, nx, nh, radius) || (!bomb && Session.Index.Actors.Any(a =>
                    a.Enemy && a.Hp > 0 && Math.Abs(a.X - nx) <= 5 + radius && nh >= a.Read().Height - radius && nh <= a.Read().Height + 18 + radius))) break;
                x = nx; h = nh;
            }
            float speed = bomb ? config.BombMinimumSpeed + (config.BombMaximumSpeed - config.BombMinimumSpeed) * Math.Clamp(charge, 0, 1) : config.BulletSpeed;
            state.Ballistics[slot] = new BallisticFlight
            {
                ViewId = state.NextViewId++, Kind = bomb ? 2 : 1, X = x, Height = h,
                VelocityX = dx * speed, VelocityY = dy * speed + (bomb ? config.BombLift : 0),
                Gravity = bomb ? config.BombGravity : 0, Radius = radius, BlastRadius = bomb ? config.ExplosionRadius : 0,
                Damage = bomb ? config.BombDamage : config.BulletDamage,
                Lifetime = bomb ? config.BombFuseSeconds : config.BulletLifetime
            };
            return true;
        }

        internal void Launch(WorldPoint from, ICombatantCapability target, int damage)
        {
            ProjectileState state = Edit();
            if (state.Shots.Length + state.Ballistics.Count(p => p.Kind != 0) >= 1024) throw new InvalidOperationException("Projectile capacity exceeded.");
            var shot = new ProjectileFlight
            {
                ViewId = state.NextViewId++, FromX = from.X, FromY = from.Y,
                ToX = target.X, ToY = Session.Layout.GroundY - ObjectCombat.Height(target) - 9,
                TargetId = target.Id, Damage = damage,
                Duration = Math.Max(0.15, Math.Abs(target.X - from.X) / 180.0)
            };
            state.Shots = state.Shots.Append(shot).ToArray();
        }

        internal void Tick(double delta)
        {
            if (Current.Shots.Length == 0 && !Current.Ballistics.Any(p => p.Kind != 0)) return;
            ProjectileState state = Edit();
            for (int i = 0; i < state.Ballistics.Length; i++)
                BallisticMotion.Tick(Session, ref state.Ballistics[i], delta, config);
            int index = 0;
            while (index < state.Shots.Length)
            {
                ProjectileFlight shot = state.Shots[index];
                shot.Age += delta;
                ICombatantCapability target = Session.Index.Find<ICombatantCapability>(shot.TargetId);
                if (target != null)
                {
                    shot.ToX = target.X;
                    shot.ToY = Session.Layout.GroundY - ObjectCombat.Height(target) - 9;
                }
                if (shot.Age < shot.Duration) { state.Shots[index++] = shot; continue; }
                state.Shots = state.Shots.Where((value, position) => position != index).ToArray();
                if (target != null && target.Hp > 0) Session.Combat.Damage(target, shot.Damage);
            }
        }
    }
}
