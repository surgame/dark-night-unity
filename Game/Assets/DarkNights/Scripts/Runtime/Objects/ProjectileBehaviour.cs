using System;
using System.Linq;
using DarkNights.Core.Logic;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 会话箭矢的唯一状态所有者，单位和塔只请求发射，飞行阶段按插入顺序结算。
    /// 目标死亡后仍完成轨迹；抵达先移除再伤害，保存复制全部未命中的记录。
    /// </summary>
    public sealed partial class ProjectileBehaviour : SessionStateBehaviour<ProjectileState>
    {
        internal void Prepare() => PrepareState(new ProjectileState());

        internal void Launch(WorldPoint from, ICombatantCapability target, int damage)
        {
            ProjectileState state = Edit();
            if (state.Shots.Length >= 1024) throw new InvalidOperationException("Projectile capacity exceeded.");
            var shot = new ProjectileFlight
            {
                ViewId = state.NextViewId++, FromX = from.X, FromY = from.Y,
                ToX = target.X, ToY = Session.Layout.GroundY - 9,
                TargetId = target.Id, Damage = damage,
                Duration = Math.Max(0.15, Math.Abs(target.X - from.X) / 180.0)
            };
            state.Shots = state.Shots.Append(shot).ToArray();
        }

        internal void Tick(double delta)
        {
            if (Current.Shots.Length == 0) return;
            ProjectileState state = Edit();
            int index = 0;
            while (index < state.Shots.Length)
            {
                ProjectileFlight shot = state.Shots[index];
                shot.Age += delta;
                ICombatantCapability target = Session.Index.Find<ICombatantCapability>(shot.TargetId);
                if (target != null)
                {
                    shot.ToX = target.X;
                    shot.ToY = Session.Layout.GroundY - 9;
                }
                if (shot.Age < shot.Duration) { state.Shots[index++] = shot; continue; }
                state.Shots = state.Shots.Where((value, position) => position != index).ToArray();
                if (target != null && target.Hp > 0) Session.Combat.Damage(target, shot.Damage);
            }
        }
    }
}
