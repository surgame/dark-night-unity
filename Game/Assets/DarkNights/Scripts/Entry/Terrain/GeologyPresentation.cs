using System;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.View.Terrain;
using UnityEngine;

namespace DarkNights.Entry.Terrain
{
    /// <summary>正式地图中塌方Prefab的只读装配；资源沿现有Definition租约创建，已知副本格与权威位图共同决定瓦砾显示，换世界时完整释放。</summary>
    internal sealed class GeologyPresentation : IDisposable
    {
        private readonly SessionNetwork network;
        private readonly Transform parent;
        private GeologyEventView view;
        private GeologySiteViewData current;
        private GeologyEventConfig config;
        private string identity = "";
        private uint previousMask;
        private int previousPhase = -1;
        private bool previousKnown;
        internal GeologyPresentation(SessionNetwork network, Transform parent) { this.network = network; this.parent = parent; }

        internal bool Present(SessionViewData frame)
        {
            var terrain = network.Terrain;
            var world = frame.World;
            var replica = terrain.Replica;
            if (world.MineralWorldId != replica.World.WorldId.ToString().Replace("-", "") || world.MineralMapEpoch != replica.World.Epoch || world.Geology.Count == 0)
            { bool changed = current != null; Dispose(); return changed; }
            var data = world.Geology[0];
            string next = data.Site.SiteId + "|" + data.Site.DefinitionHash + "|" + data.Site.Configuration;
            if (identity != next)
            {
                Dispose();
                var created = network.ObjectResources.CreateGeologyView(data.Site, parent);
                view = created as GeologyEventView;
                if (view == null) { network.ObjectResources.ReleaseGeologyView(created); throw new InvalidOperationException("塌方Definition未引用GeologyEventView。"); }
                config = GeologySiteConfiguration.Resolve(data.Site);
                view.Bind(Enumerable.Range(0, config.RubbleCount).Select(config.Motion).ToArray(),
                    Enumerable.Range(0, config.RubbleCount).Select(config.PileShape).ToArray(), config.Collapse);
                identity = next;
            }
            current = data;
            bool known = Enumerable.Range(0, config.RockCount).All(i => replica.Read(config.Source(i)).TryGetCell(out _)) &&
                Enumerable.Range(0, config.RubbleCount).All(i => replica.Read(config.Landing(i)).TryGetCell(out _));
            view.gameObject.SetActive(known);
            uint mask = 0;
            if (known && data.State.Phase == 4)
                for (int i = 0; i < config.RubbleCount; i++)
                    if ((data.State.SoilMask & 1u << i) != 0 && replica.Read(config.Landing(i)).TryGetCell(out var cell) &&
                        cell.TileId == terrain.Tiles.ByKey(config.Material) && cell.Flags == 0) mask |= 1u << i;
            if (known) view.Present(new GeologyEventViewData(data.State.Phase, data.State.Clock, data.State.Reason, mask), frame.Elapsed);
            bool rebuild = previousPhase != data.State.Phase || previousMask != mask || previousKnown != known;
            previousPhase = data.State.Phase; previousMask = mask; previousKnown = known;
            return rebuild;
        }

        internal GridCell Project(CellCoord position, GridCell cell)
        {
            if (current == null || view == null || !view.gameObject.activeSelf || current.State.Phase < 2 || current.State.Phase > 4 || config == null || cell.IsEmpty) return cell;
            if (cell.TileId != network.Terrain.Tiles.ByKey(config.Material) || cell.Flags != 0) return cell;
            for (int i = 0; i < config.RubbleCount; i++) if (config.Landing(i).Equals(position)) return default;
            return cell;
        }
        public void Dispose()
        {
            if (view != null) network.ObjectResources.ReleaseGeologyView(view);
            view = null; current = null; config = null; identity = ""; previousPhase = -1; previousMask = 0; previousKnown = false;
        }
    }
}
