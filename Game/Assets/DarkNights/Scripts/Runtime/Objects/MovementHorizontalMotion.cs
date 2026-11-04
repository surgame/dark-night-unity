using System;
using DarkNights.Core.Config;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 地面启停、预设冲刺过渡与有限空控的共用计算，玩家与自动移动使用同一速度和起跳参考。
    /// 输入朝向与实际速度独立，反打先刹停再有限反向；碰撞方负责裁切位移和清除受阻速度。
    /// </summary>
    public static class MovementHorizontalMotion
    {
        public static void Prepare(ActorState state, HeroControlDefinition rules,
            MovementDefinition movement, bool grounded, double delta, float speedMultiplier = 1,
            float? direction = null, float? normalSpeed = null)
        {
            float axis = direction ?? state.Horizontal;
            if (axis != 0) state.Face = Math.Sign(axis);
            float walkSpeed = normalSpeed ?? movement.WalkSpeed(rules) * speedMultiplier;
            if (grounded)
            {
                float groundTarget = axis * (normalSpeed ?? movement.MoveSpeed(rules, state.SprintHeld) * speedMultiplier);
                state.HorizontalSpeed = normalSpeed.HasValue || movement.Speed == null
                    ? groundTarget : GroundSpeed(state, movement.Speed, axis, groundTarget, walkSpeed, speedMultiplier, (float)delta);
                state.TakeoffHorizontalSpeed = state.HorizontalSpeed;
                return;
            }
            float reference = Math.Max(walkSpeed, Math.Abs(state.TakeoffHorizontalSpeed));
            float time = (float)delta;
            if (axis == 0 || !movement.EnableAirControl)
            {
                state.HorizontalSpeed = Toward(state.HorizontalSpeed, 0, reference * movement.AirDragPerSecond * time);
                return;
            }
            float correction = walkSpeed * movement.AirSpeedRatio;
            float target = axis * Math.Max(correction, axis * state.TakeoffHorizontalSpeed);
            if (axis * state.HorizontalSpeed < 0)
            {
                float braking = reference / movement.AirBrakingSeconds;
                float stoppingTime = Math.Abs(state.HorizontalSpeed) / braking;
                state.HorizontalSpeed = Toward(state.HorizontalSpeed, 0, braking * time);
                if (stoppingTime >= time) return;
                time -= stoppingTime;
            }
            state.HorizontalSpeed = Toward(state.HorizontalSpeed, target, correction / movement.AirAccelerationSeconds * time);
        }

        public static void Land(ActorState state, HeroControlDefinition rules, float speedMultiplier = 1,
            float? direction = null, float? normalSpeed = null, MovementDefinition movement = null)
        {
            // 预设方案保留本步已推进或离地继承的真实速度，避免每次接地再跳回冲刺上限。
            if (normalSpeed.HasValue || movement?.Speed == null)
                state.HorizontalSpeed = (direction ?? state.Horizontal) *
                    (normalSpeed ?? rules.MoveSpeed(state.SprintHeld) * speedMultiplier);
            state.TakeoffHorizontalSpeed = 0;
        }

        public static void Clear(ActorState state)
        {
            state.HorizontalSpeed = 0;
            state.TakeoffHorizontalSpeed = 0;
        }

        private static float GroundSpeed(ActorState state, MovementSpeedDefinition speed, float axis,
            float target, float walkSpeed, float multiplier, float delta)
        {
            if (axis == 0) return 0;
            float current = Math.Max(walkSpeed, axis * state.HorizontalSpeed);
            float seconds = state.SprintHeld ? speed.SprintAccelerationSeconds : speed.SprintDecelerationSeconds;
            float rate = (speed.SprintSpeed - speed.WalkSpeed) * multiplier / seconds;
            return axis * Toward(current, Math.Abs(target), rate * delta);
        }

        public static float Target(ActorState state, HeroControlDefinition rules, MovementDefinition movement,
            bool grounded, double delta, float maximumX, float speedMultiplier = 1,
            float? direction = null, float? normalSpeed = null, float? goalX = null)
        {
            Prepare(state, rules, movement, grounded, delta, speedMultiplier, direction, normalSpeed);
            float requested = state.X + state.HorizontalSpeed * (float)delta;
            float target = Math.Clamp(requested, 16, maximumX);
            if (target != requested) state.HorizontalSpeed = 0;
            if (grounded && goalX.HasValue && (requested - state.X) * (goalX.Value - state.X) >= 0 &&
                Math.Abs(requested - state.X) > Math.Abs(goalX.Value - state.X))
            {
                target = Math.Clamp(goalX.Value, 16, maximumX);
                state.HorizontalSpeed = 0;
            }
            return target;
        }

        private static float Toward(float value, float target, float amount) =>
            value < target ? Math.Min(target, value + amount) : Math.Max(target, value - amount);
    }
}
