using System;
using System.Collections.Generic;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.ViewData;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Objects
{
    /// <summary>正式ObjectSession中地质对象的装配与引用集合；每事件状态归原有Behaviour，换图和恢复在同一事务切换引用，旧对象只在提交后释放。</summary>
    public sealed class SessionGeology : IDisposable
    {
        private readonly ObjectSession world;
        private readonly List<GeologyEventBehaviour> events = new List<GeologyEventBehaviour>();
        private TerrainGeologySite[] sites = Array.Empty<TerrainGeologySite>();
        internal SessionGeology(ObjectSession world) { this.world = world; }
        internal void Replace(PlayableTerrain map)
        {
            world.Mutations.RequireWriting();
            var previous = events.ToArray(); var previousSites = sites;
            var next = new List<GeologyEventBehaviour>();
            world.Mutations.OnRollback(() =>
            {
                events.Clear(); events.AddRange(previous); sites = previousSites;
                foreach (var item in next) world.ReleaseEntity(item.Object);
            });
            foreach (var site in map.GeologySites)
            {
                var definition = world.Resources.GeologyDefinition(site);
                var instance = world.Resources.Create(definition, world.Context, world.Parent).Owner;
                var behaviour = instance.GetBehaviour<GeologyEventBehaviour>();
                if (behaviour == null) { world.ReleaseEntity(instance); throw new InvalidOperationException("塌方模板缺少地质能力。"); }
                next.Add(behaviour);
                behaviour.Prepare(GeologySiteConfiguration.Resolve(site), map.GeologyProgress.FirstOrDefault(p => p.SiteId == site.SiteId));
                instance.Activate(); instance.gameObject.SetActive(false);
            }
            events.Clear(); events.AddRange(next); sites = map.GeologySites.ToArray();
            world.Mutations.AfterCommit(() => { foreach (var item in previous) world.ReleaseEntity(item.Object); });
        }
        internal void Tick(double delta) { foreach (var item in events) item.Tick(delta); }
        internal bool ReservesSource(CellCoord cell) => events.Any(item => item.ReservesSource(cell));
        internal TerrainGeologyProgress[] CaptureProgress() => events.Select(item =>
        {
            var value = item.Read(); return new TerrainGeologyProgress(item.Configuration.EventId, value.Phase, value.Clock, value.Reason);
        }).ToArray();
        internal GeologySiteViewData[] CaptureView() => events.Select((item, index) => new GeologySiteViewData(sites[index], item.Capture())).ToArray();
        public void Dispose()
        {
            foreach (var item in events) world.ReleaseEntity(item.Object);
            events.Clear(); sites = Array.Empty<TerrainGeologySite>();
        }
    }
}
