using System;
using System.Collections.Generic;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.View.Terrain;
using UnityEngine;

namespace DarkNights.Entry.Terrain
{
    /// <summary>同一前景副本世界内的巢穴被动表现集合；按冻结编号创建原生Prefab，未知地图区域不显示，换世界或退出完整退休。</summary>
    internal sealed class NestPresentation : IDisposable
    {
        private readonly NestEncounterView template;
        private readonly Transform parent;
        private readonly Dictionary<int, NestEncounterView> views = new Dictionary<int, NestEncounterView>();
        internal NestPresentation(NestEncounterView template, Transform parent) { this.template = template; this.parent = parent; }
        internal void Present(WorldViewData world, Runtime.Terrain.SessionTerrainNetwork terrain)
        {
            if (world.Nests.Count != 0 && template == null) throw new InvalidOperationException("当前洞穴样式没有巢穴Prefab引用。");
            var replica = terrain.Replica;
            var wanted = new HashSet<int>();
            if (world.MineralWorldId == replica.World.WorldId.ToString().Replace("-", "") && world.MineralMapEpoch == replica.World.Epoch)
                foreach (var data in world.Nests)
                {
                    int u = (int)Math.Floor(data.X / 16 + .5f), row = (int)Math.Floor((PlayableTerrain.OriginY - data.Height - 6) / 16 + .5f);
                    if (!replica.Read(new CellCoord(u, -row)).TryGetCell(out _)) continue;
                    wanted.Add(data.Id);
                    if (!views.TryGetValue(data.Id, out var view))
                    { view = UnityEngine.Object.Instantiate(template, parent); view.name = "Nest " + data.Id; views.Add(data.Id, view); }
                    view.Present(data);
                }
            foreach (int id in views.Keys.Where(id => !wanted.Contains(id)).ToArray())
            { views[id].gameObject.SetActive(false); UnityEngine.Object.Destroy(views[id].gameObject); views.Remove(id); }
        }
        public void Dispose()
        {
            foreach (var view in views.Values) if (view != null) { view.gameObject.SetActive(false); UnityEngine.Object.Destroy(view.gameObject); }
            views.Clear();
        }
    }
}
