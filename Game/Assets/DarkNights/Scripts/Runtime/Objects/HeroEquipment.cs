using System;
using DarkNights.Core.Config;

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
            if (state.PickaxeSwingActive) state.EquipmentAction = 0;
            state.PickaxeSwingActive = state.PickaxeHitPending = false;
            state.PickaxeSwingTarget = default; state.PickaxeSwingAim = 0;
            state.MiningToolDefinition = ""; state.MiningToolSelectionRevision = 0;
            state.Charging = false; state.ChargeSeconds = 0;
            state.UsePressed = state.UseReleased = state.UseHeld = false;
            state.MiningWorldId = null; state.MiningMapEpoch = 0;
            state.MiningU = state.MiningV = 0; state.MiningTileId = 0; state.MiningFlags = 0;
            state.MiningTargetKind = DarkNights.Core.ViewData.HeroMiningTargetKind.None;
            state.MiningEntityId = 0; state.MiningContentVersion = 0;
            state.MiningMineralContentVersion = 0;
        }

        internal static void Tick(ActorBehaviour actor, double delta)
        {
            ActorState state = actor.Edit();
            HandheldConfig config = actor.World.Projectiles.Settings;
            state.EquipmentCooldown = Math.Max(0, state.EquipmentCooldown - delta);
            state.EquipmentAction = Math.Max(0, state.EquipmentAction - delta);
            bool pressed = state.UsePressed, released = state.UseReleased;
            state.UsePressed = state.UseReleased = false;
            if (!state.ManualControl || state.ControllerSlot < 0 || state.Hp <= 0 || state.Boarded ||
                actor.World.IsExpedition && !actor.World.Expedition.Active) { Cancel(state); return; }
            string definitionGuid = HeroInventoryBehaviour.Slot(state, state.SelectedItem);
            HeroEquipmentKind item = actor.World.Resources.Equipment.Kind(definitionGuid);
            if (item != HeroEquipmentKind.Empty)
                state.Face = Math.Cos((state.PickaxeSwingActive ? state.PickaxeSwingAim : state.AimAngle) * Math.PI / 180) < 0 ? -1 : 1;
            var mining = actor.Object.GetBehaviour<HeroInventoryBehaviour>().MiningTool();
            if (mining != null)
            {
                TickPickaxe(actor, state, mining, definitionGuid, pressed);
                return;
            }
            if (item == HeroEquipmentKind.Bomb)
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
            if (item == HeroEquipmentKind.Empty || state.EquipmentCooldown > 0 || (!state.UseHeld && !pressed)) return;
            if (item == HeroEquipmentKind.Pistol)
            {
                if (!actor.World.Projectiles.LaunchHandheld(state, false, 0)) return;
                state.EquipmentCooldown = config.FireInterval; state.EquipmentAction = 0.12;
            }
            else return;
            state.EquipmentActionDuration = state.EquipmentAction;
        }

        private static void TickPickaxe(ActorBehaviour actor, ActorState state, MiningToolBehaviour tool, string definitionGuid, bool pressed)
        {
            var config = tool.Rules;
            if (state.PickaxeSwingActive && (state.MiningToolDefinition != definitionGuid ||
                state.MiningToolSelectionRevision != state.SelectionRevision)) Cancel(state);
            if (state.PickaxeSwingActive)
            {
                if (state.PickaxeHitPending && state.EquipmentAction <= config.Seconds * (1 - config.ImpactFraction))
                {
                    state.PickaxeHitPending = false;
                    tool.Hit(actor, state.PickaxeSwingTarget, state.PickaxeSwingAim);
                }
                if (state.EquipmentAction > 0) return;
                state.PickaxeSwingActive = false; state.PickaxeSwingTarget = default;
            }
            if (state.EquipmentCooldown > 0 || (!state.UseHeld && !pressed)) return;
            state.PickaxeSwingTarget = HeroMining.InputTarget(state);
            state.MiningToolDefinition = definitionGuid; state.MiningToolSelectionRevision = state.SelectionRevision;
            state.PickaxeSwingAim = state.AimAngle;
            state.Face = Math.Cos(state.PickaxeSwingAim * Math.PI / 180) < 0 ? -1 : 1;
            state.PickaxeSwingActive = state.PickaxeHitPending = true;
            state.EquipmentCooldown = state.EquipmentAction = state.EquipmentActionDuration = config.Seconds;
        }
    }
}
