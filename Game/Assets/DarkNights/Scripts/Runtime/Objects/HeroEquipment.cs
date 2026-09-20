using System;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 主角道具的同步业务步骤；状态仍完全属于 ActorState，投射物属于 ProjectileBehaviour。
    /// 蓄力仅累计服务端模拟秒，释放才投掷；取消、换装或失去控制均清空蓄力，不产生补发。
    /// </summary>
    internal static class HeroEquipment
    {
        internal static void Cancel(ActorState state)
        {
            state.Charging = false; state.ChargeSeconds = 0;
            state.UsePressed = state.UseReleased = state.UseHeld = false;
        }

        internal static void Tick(ActorBehaviour actor, double delta)
        {
            ActorState state = actor.Edit();
            HandheldConfig config = actor.World.Projectiles.Settings;
            state.EquipmentCooldown = Math.Max(0, state.EquipmentCooldown - delta);
            state.EquipmentAction = Math.Max(0, state.EquipmentAction - delta);
            bool pressed = state.UsePressed, released = state.UseReleased;
            state.UsePressed = state.UseReleased = false;
            if (!state.ManualControl || state.ControllerSlot < 0) { Cancel(state); return; }
            if (state.SelectedItem < 3)
                state.Face = Math.Cos(state.AimAngle * Math.PI / 180) < 0 ? -1 : 1;
            if (state.SelectedItem == 2)
            {
                if (pressed && state.EquipmentCooldown == 0 && state.ExplosiveCharges > 0) state.Charging = true;
                if (state.Charging && state.UseHeld)
                    state.ChargeSeconds = Math.Min(config.BombChargeSeconds, state.ChargeSeconds + delta);
                if (released && state.Charging)
                {
                    if (actor.World.Projectiles.LaunchHandheld(state, true, (float)(state.ChargeSeconds / config.BombChargeSeconds)))
                    { state.ExplosiveCharges--; state.EquipmentCooldown = config.BombCooldown; state.EquipmentAction = 0.25; state.EquipmentActionDuration = 0.25; }
                    Cancel(state);
                }
                return;
            }
            if (state.SelectedItem == 3 || state.EquipmentCooldown > 0 || (!state.UseHeld && !pressed)) return;
            if (state.SelectedItem == 0)
            {
                if (!actor.World.Projectiles.LaunchHandheld(state, false, 0)) return;
                state.EquipmentCooldown = config.FireInterval; state.EquipmentAction = 0.12;
            }
            else
            {
                HeroMining.TryMine(actor);
                state.EquipmentCooldown = config.PickaxeSeconds;
                state.EquipmentAction = config.PickaxeSeconds;
            }
            state.EquipmentActionDuration = state.EquipmentAction;
        }
    }
}
