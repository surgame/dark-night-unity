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
        private readonly float handHeight;
        private CellCoord cell;
        private bool visible, valid;
        public HeroMiningTarget Target { get; private set; }
        public string Hint { get; private set; } = "";

        internal HeroMiningPointer(SessionNetwork network, PinewatchStage stage, HeroHudBehaviour hud, GameCatalog catalog)
        {
            this.network = network; this.stage = stage; this.hud = hud; this.catalog = catalog;
            handHeight = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch")
                .SharedConfigs.OfType<HandheldConfig>().Single().HandHeight;
            hud.InitializeMiningSelector();
        }

        internal void Sample(GameInputActions.HeroFrame controls, ActorViewData actor, SessionViewData frame, bool selectionPending)
        {
            Target = default; visible = valid = false; Hint = "";
            var terrain = network.Terrain;
            bool aboard = frame?.World.Expedition?.Crew.Any(value => value.Id == actor?.Id && value.Boarded) == true;
            if (!controls.Allowed || !controls.UseAllowed || selectionPending || !network.Client.Ready ||
                terrain?.DataReady != true || !terrain.PresentationReady || actor == null || actor.Hp <= 0 ||
                frame == null || frame.Paused || aboard || Slot(actor) != 2 ||
                frame.World.Expedition != null && frame.World.Expedition.Phase != 1 && frame.World.Expedition.Phase != 2 ||
                frame.HostOnly && network.Client.PlayerSlot != 0 || !stage.SceneCamera.pixelRect.Contains(controls.Pointer)) return;
            Vector3 point = stage.SceneCamera.ScreenToWorldPoint(new Vector3(controls.Pointer.x, controls.Pointer.y,
                stage.SceneCamera.WorldToScreenPoint(Vector3.zero).z));
            cell = new CellCoord(TerrainMiningGeometry.CellU(point.x * 100), TerrainMiningGeometry.CellV(point.y * 100));
            var map = terrain.Replica;
            if (map.Descriptor == null || !map.Descriptor.Bounds.Contains(cell) || !map.Read(cell).TryGetCell(out var value)) return;
            visible = true;
            var deposit = frame.World.Worksites.FirstOrDefault(site => site.IsMineralDeposit && site.Amount > 0 &&
                (int)Math.Floor(site.X / PlayableTerrain.CellPixels) == cell.U && -(int)site.Y == cell.V);
            string blocked = value.IsEmpty && deposit != null ? "" : TerrainMiningQuery.BlockReason(map, terrain.Tiles, cell);
            var cargo = frame.World.Expedition?.Crew.FirstOrDefault(crew => crew.Id == actor.Id);
            bool capacity = cargo == null || cargo.Iron + cargo.Gold < catalog.Balance.Expedition.BagCapacity;
            if (blocked.Length == 0 && !capacity) blocked = "货袋已满，请先出售或卸货";
            if (blocked.Length == 0 && !TerrainMiningGeometry.WithinReach(actor.X, actor.Height + handHeight,
                cell.U, cell.V, catalog.Balance.HeroControl.WorkReach)) blocked = "目标太远，请靠近后采集";
            if (blocked.Length == 0 && !TerrainMiningQuery.Reachable(map, actor.X, actor.Height + handHeight,
                cell, catalog.Balance.HeroControl.WorkReach)) blocked = "目标被岩壁遮挡";
            valid = blocked.Length == 0;
            Hint = valid ? "左键采集 · 按住连续采集" : blocked;
            if (valid) Target = new HeroMiningTarget(map.World.WorldId.ToString().Replace("-", ""), map.World.Epoch,
                cell.U, cell.V, value.TileId, value.Flags);
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
