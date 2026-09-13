using System;
using System.Linq;
using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 显式压力验收使用的合成展示帧，沿用正式可靠状态链并覆盖支持上限；不修改权威世界或存档。
    /// 只有 dn-projection-pressure 参数启用，结果必须标为合成传输／显示负载，不能冒充真实三夜玩法。
    /// </summary>
    public static class ProjectionPressure
    {
        public static SessionViewData Expand(SessionViewData frame)
        {
            var wire = SessionWire.From(frame);
            int count = frame.World.Identities.Count;
            int nextId = frame.World.Identities.Max(i => i.Id) + 1;
            string actorGuid = frame.World.Identities.Single(i => i.Id == frame.World.Actors[0].Id).DefinitionGuid;
            var additional = Enumerable.Range(nextId, WorldViewData.MaximumEntities - count).Select(id =>
            {
                var actor = ActorWire.From(frame.World.Actors[0]);
                actor.Id = id;
                actor.Name = id.ToString().PadRight(256, '工');
                actor.X = 30 + id * 3;
                return actor;
            }).ToArray();
            wire.World.Actors = wire.World.Actors.Concat(additional).ToArray();
            wire.World.Identities = wire.World.Identities.Concat(additional.Select(a => new EntityIdentityWire
                { Id = a.Id, DefinitionGuid = actorGuid, PlacementKey = "" })).ToArray();
            wire.World.Projectiles = Enumerable.Range(1, 1024).Select(id => new ProjectileWire
            {
                ViewId = id, FromX = 50, FromY = 290, ToX = 500, ToY = 290, Age = .2, Duration = 1
            }).ToArray();
            wire.Events = Enumerable.Range(1, SessionViewData.MaximumEvents).Select(id => PresentationWire.From(
                new PresentationEvent(id, frame.ServerTick, "banner", new string('横', 256), new string('幅', 256)))).ToArray();
            wire.Remnants = Enumerable.Range(SessionViewData.MaximumEvents + 1, SessionViewData.MaximumRemnants).Select(id => PresentationWire.From(
                new PresentationEvent(id, frame.ServerTick, "effect", cue: new VisualCue("rubble", 100, 320, ContentId: "barracks")))).ToArray();
            return wire.Freeze();
        }
    }
}
