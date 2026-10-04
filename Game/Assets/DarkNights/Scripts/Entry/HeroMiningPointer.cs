using System;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using DarkNights.View;
using GameCore.Objects.Definition;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>本地矿镐目标采样与只读有效性反馈；仅将冻结目标交给原主角输入流，最终授权仍由服务器决定。</summary>
    internal sealed class HeroMiningPointer
    {
        private readonly SessionNetwork network;
        private readonly PinewatchStage stage;
        private readonly HeroHudBehaviour hud;
        private readonly GameCatalog catalog;
        private MiningToolRules tool;
        private float handHeight => tool?.HandHeight ?? 0;
        private float reach => tool?.Reach ?? 0;
        private int pickaxeDamage => tool?.Damage ?? 0;
        private CellCoord cell;
        private bool visible, valid;
        public HeroMiningTarget Target { get; private set; }
        public string Hint { get; private set; } = "";
        internal float HandHeight => handHeight;

        internal HeroMiningPointer(SessionNetwork network, PinewatchStage stage, HeroHudBehaviour hud, GameCatalog catalog)
        {
            this.network = network; this.stage = stage; this.hud = hud; this.catalog = catalog;
            hud.InitializeMiningSelector();
        }

        internal void Sample(GameInputActions.HeroFrame controls, ActorViewData actor, SessionViewData frame, bool selectionPending)
        {
            Target = default; visible = valid = false; Hint = "";
            tool = actor == null ? null : network.ObjectResources.Equipment.Mining(actor.SelectedItem switch
            { 0 => actor.Slot0Definition, 1 => actor.Slot1Definition, 2 => actor.Slot2Definition, 3 => actor.Slot3Definition, _ => "" });
            var terrain = network.Terrain;
            bool aboard = frame?.World.Expedition?.Crew.Any(value => value.Id == actor?.Id && value.Boarded) == true;
            if (!controls.Allowed || !controls.UseAllowed || selectionPending || !network.Client.Ready ||
                terrain?.DataReady != true || !terrain.PresentationReady || actor == null || actor.Hp <= 0 ||
                frame == null || frame.Paused || aboard || tool == null ||
                frame.World.Expedition != null && frame.World.Expedition.Phase != 1 && frame.World.Expedition.Phase != 2 ||
                frame.HostOnly && network.Client.PlayerSlot != 0 || !stage.SceneCamera.pixelRect.Contains(controls.Pointer)) return;
            Vector3 point = stage.SceneCamera.ScreenToWorldPoint(new Vector3(controls.Pointer.x, controls.Pointer.y,
                stage.SceneCamera.WorldToScreenPoint(Vector3.zero).z));
            var map = terrain.Replica;
            float hand = actor.Height + handHeight;
            if (map.Descriptor == null || !TerrainMiningGeometry.Direction(point.x * 100 - actor.X,
                point.y * 100 - hand, out float dx, out float dh)) return;
            bool found = TerrainMiningQuery.FirstSurface(map, actor.X, hand, dx, dh, reach, out cell, out float distance);
            MineralDepositViewData deposit = null;
            MineralCellViewData mineralCell = default;
            foreach (var site in frame.World.MineralDeposits)
            {
                if (site.Remaining <= 0 || tool.BlockReason(HeroMiningTargetKind.MineralDeposit,
                    site.ResourceId, site.RequiredMiningLevel, frame.World.Identities.Single(value => value.Id == site.Id).DefinitionGuid).Length > 0) continue;
                foreach (var mineral in site.Cells)
                {
                    var position = new CellCoord(mineral.U, mineral.V);
                    if (mineral.Remaining <= 0 || !map.Read(position).TryGetCell(out var background) || !background.IsEmpty ||
                        !TerrainMiningGeometry.RayCell(actor.X, hand, dx, dh, reach, position.U, position.V,
                            TerrainCellShape.Full, out float near) || near >= distance) continue;
                    found = true; cell = position; distance = near; deposit = site; mineralCell = mineral;
                }
            }
            if (!found) { Hint = "沿鼠标方向没有可触及的采集目标 · 按住左键挥镐"; return; }
            visible = true;
            if (!map.Descriptor.Bounds.Contains(cell) || !map.Read(cell).TryGetCell(out var value))
            { Hint = "地图尚未就绪"; return; }
            string blocked = value.IsEmpty && deposit != null ? "" : TerrainMiningQuery.BlockReason(map, terrain.Tiles, cell);
            if (!value.IsEmpty && blocked.Length == 0)
                blocked = tool.BlockReason(HeroMiningTargetKind.Foreground, terrain.Rules.Material(value.TileId));
            var cargo = frame.World.Expedition?.Crew.FirstOrDefault(crew => crew.Id == actor.Id);
            int durability = 0, maximum = 0, amount = 0, damage = pickaxeDamage;
            if (!value.IsEmpty && blocked.Length == 0)
            {
                var sample = map.Query(cell); durability = sample.State.Durability; maximum = sample.Definition.MaximumDurability;
                amount = terrain.Rules.Drop(value.TileId).Amount; damage = terrain.Rules.PickaxeDamage(value.TileId, pickaxeDamage);
            }
            else if (deposit != null)
            {
                durability = mineralCell.Durability; maximum = deposit.MaximumDurability; amount = Math.Min(mineralCell.Remaining, deposit.UnitsPerHarvest);
            }
            bool capacity = cargo == null || cargo.Iron + cargo.Gold + amount <= catalog.Balance.Expedition.BagCapacity;
            if (blocked.Length == 0 && durability <= damage && !capacity) blocked = "完成采集需要货袋空间，请先卸货";
            valid = blocked.Length == 0;
            Hint = valid ? (value.IsEmpty ? "矿床采集" : "岩壁耐久") + " " + durability + "/" + maximum + " · 左键／按住采集" : blocked;
            if (valid) Target = new HeroMiningTarget(map.World.WorldId.ToString().Replace("-", ""), map.World.Epoch,
                cell.U, cell.V, value.TileId, value.Flags,
                value.IsEmpty ? HeroMiningTargetKind.MineralDeposit : HeroMiningTargetKind.Foreground,
                value.IsEmpty ? deposit.Id : 0, map.ContentVersion(cell), value.IsEmpty ? mineralCell.ContentVersion : 0);
        }

        internal void Present()
        {
            hud.PresentMining(stage.SceneCamera, new Vector3(TerrainMiningGeometry.CenterX(cell.U) / 100,
                TerrainMiningGeometry.CenterHeight(cell.V) / 100, 0), stage.MiningSelector, visible, valid);
        }

        internal void Hide()
        {
            visible = valid = false; Target = default; Hint = ""; Present();
        }

        private static int Slot(ActorViewData actor) => actor.SelectedItem switch
        { 0 => actor.Slot0, 1 => actor.Slot1, 2 => actor.Slot2, 3 => actor.Slot3, _ => 0 };
    }
}
