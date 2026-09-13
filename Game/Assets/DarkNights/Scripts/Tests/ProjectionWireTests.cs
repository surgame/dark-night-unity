using System;
using System.IO;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine.TestTools;
using System.Linq;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using GameCore.NetworkCommands;
using GameCore.Objects.NetworkStates;
using MemoryPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>
    /// 使用实际 MemoryPack 与 YYGC 状态池验证完整投影往返、归池隔离、非法输入和支持上限的字节数。
    /// 测量属于 Editor 编码器探针，不能代替独立进程可靠传输、Player 性能或画面验收。
    /// </summary>
    [Category("UnifiedSession")]
    public sealed class ProjectionWireTests
    {
        private UnifiedSessionScope scope;

        [UnitySetUp]
        public IEnumerator Prepare()
        {
            yield return UniTask.ToCoroutine(async () => scope = await UnifiedSessionScope.Create());
        }

        [TearDown]
        public void Cleanup() => scope?.Dispose();

        [Test]
        public void RealBattleSnapshotsRemainDecodableAfterWindupCrossesZero()
        {
            RuleScenario.RepositoryRoot = Path.GetFullPath("..");
            var catalog = RuleScenario.Catalog();
            var layout = RuleScenario.Layout();
            using var authority = SessionScenario.Open(catalog, layout, out var host, out _);
            var codec = new ProjectionCodec(catalog, layout);
            SessionScenario.Execute(authority, host, SessionScenario.Request(authority, SessionOperation.StartNight, 1));
            SessionScenario.Execute(authority, host, SessionScenario.Request(authority, SessionOperation.SetSpeed, 2, value: 2));
            bool negativeWindup = false, projectile = false, death = false;
            for (int tick = 0; tick < 7200; tick++)
            {
                authority.Tick();
                if (tick % 6 != 0) continue;
                var frame = authority.CaptureProjection();
                var decoded = codec.Decode(codec.Encode(frame));
                negativeWindup |= decoded.World.Actors.Any(a => a.Windup < 0);
                projectile |= decoded.World.Projectiles.Count > 0;
                death |= decoded.Events.Any(e => e.Type == "effect" && e.Cue.Kind == "corpse");
            }
            Assert.IsTrue(negativeWindup, "Must exercise the original negative windup remainder.");
            Assert.IsTrue(projectile, "Must exercise real projectile snapshots.");
            Assert.IsTrue(death, "Must exercise real death notifications.");
        }

        [Test]
        public void RealCodecFreezesNestedStateAndRejectsMalformedFrames()
        {
            RuleScenario.RepositoryRoot = Path.GetFullPath("..");
            var catalog = RuleScenario.Catalog();
            var layout = RuleScenario.Layout();
            using var authority = SessionScenario.Open(catalog, layout, out var host, out _);
            var codec = new ProjectionCodec(catalog, layout);
            var initial = authority.CaptureProjection();
            var encoded = codec.Encode(initial);
            var decoded = codec.Decode(encoded);
            Assert.That(JsonConvert.SerializeObject(decoded), Is.EqualTo(JsonConvert.SerializeObject(initial)));
            var mutable = SessionWire.From(initial);
            var frozen = mutable.Freeze();
            mutable.World.Actors[0].Hp = 0;
            mutable.World.Actors = Array.Empty<ActorWire>();
            Assert.That(frozen.World.Actors.Count, Is.EqualTo(7));
            Assert.That(frozen.World.Actors[0].Hp, Is.GreaterThan(0));
            Assert.Throws<FormatException>(() => codec.Decode(encoded.Concat(new byte[] { 0 }).ToArray()));
            Assert.Throws<FormatException>(() => codec.Decode(new byte[ProjectionCodec.MaximumBytes + 1]));
            Assert.Catch(() => codec.Decode(encoded.Take(encoded.Length / 2).ToArray()));
            mutable = SessionWire.From(initial);
            mutable.World.Identities = Array.Empty<EntityIdentityWire>();
            Assert.Throws<FormatException>(() => codec.Decode(MemoryPackSerializer.Serialize(mutable)));
            mutable = SessionWire.From(initial);
            mutable.World.Identities[0].DefinitionGuid = "";
            Assert.Throws<FormatException>(() => codec.Decode(MemoryPackSerializer.Serialize(mutable)));
            mutable = SessionWire.From(initial);
            mutable.World.Actors[0].X = float.NaN;
            Assert.Throws<FormatException>(() => codec.Decode(MemoryPackSerializer.Serialize(mutable)));
            mutable = SessionWire.From(initial);
            mutable.World.Actors[0].Kind = "unknown";
            Assert.Throws<FormatException>(() => codec.Decode(MemoryPackSerializer.Serialize(mutable)));
            mutable = SessionWire.From(initial);
            mutable.World.Camp.NextSpawn = catalog.Level.Waves[0].Enemies.Count + 1;
            Assert.Throws<FormatException>(() => codec.Decode(MemoryPackSerializer.Serialize(mutable)));
            mutable.World.Camp.NextSpawn = -1;
            Assert.Throws<FormatException>(() => codec.Decode(MemoryPackSerializer.Serialize(mutable)));
            mutable = SessionWire.From(initial);
            mutable.Events[0].Type = "unknown";
            Assert.Throws<FormatException>(() => codec.Decode(MemoryPackSerializer.Serialize(mutable)));
            mutable = SessionWire.From(initial);
            mutable.Events[0].Tick = initial.ServerTick + 1;
            Assert.Throws<FormatException>(() => codec.Decode(MemoryPackSerializer.Serialize(mutable)));
            mutable = SessionWire.From(initial);
            mutable.Events[0].Text = new string('x', 257);
            Assert.Throws<FormatException>(() => codec.Decode(MemoryPackSerializer.Serialize(mutable)));
            mutable = SessionWire.From(initial);
            mutable.World.Buildings[0].Id = mutable.World.Actors[0].Id;
            Assert.Throws<ArgumentException>(() => codec.Decode(MemoryPackSerializer.Serialize(mutable)));
        }

        [Test]
        public void PooledStateReturnCannotRewriteOwnedReplica()
        {
            RuleScenario.RepositoryRoot = Path.GetFullPath("..");
            var catalog = RuleScenario.Catalog();
            var layout = RuleScenario.Layout();
            using var authority = SessionScenario.Create(catalog, layout);
            var codec = new ProjectionCodec(catalog, layout);
            var first = GenericTypePool<SessionStatusState>.Get();
            first.ProjectionPayload = codec.Encode(authority.CaptureProjection());
            var copied = GenericTypePool<SessionStatusState>.Get();
            copied.CopyFrom(first);
            var frozen = codec.Decode(copied.ProjectionPayload);
            string baseline = JsonConvert.SerializeObject(frozen);
            GenericTypePool<SessionStatusState>.Return(first);
            Assert.That(codec.Decode(copied.ProjectionPayload).World.Actors.Count, Is.EqualTo(7));
            GenericTypePool<SessionStatusState>.Return(copied);
            var reused = GenericTypePool<SessionStatusState>.Get();
            reused.ProjectionPayload = new byte[] { 0 };
            Assert.That(JsonConvert.SerializeObject(frozen), Is.EqualTo(baseline));
            GenericTypePool<SessionStatusState>.Return(reused);
            Assert.That(File.ReadAllText("Assets/Scripts/Generated/INetworkCommand.generated.cs"),
                Does.Contain("SessionCommand>(1)").And.Contain("SetReadyCommand>(0)"));
        }

        [Test]
        public void MeasureCompleteProjectionAtSupportedLimits()
        {
            RuleScenario.RepositoryRoot = Path.GetFullPath("..");
            var catalog = RuleScenario.Catalog();
            var layout = RuleScenario.Layout();
            using var authority = SessionScenario.Create(catalog, layout);
            var codec = new ProjectionCodec(catalog, layout);
            var first = authority.CaptureProjection();
            var source = SessionWire.From(first);
            source.World.Actors = Enumerable.Range(1, 256).Select(id =>
            {
                var actor = ActorWire.From(first.World.Actors[0]);
                actor.Id = id;
                actor.Name = new string('工', 256);
                return actor;
            }).ToArray();
            string actorGuid = first.World.Identities.Single(i => i.Id == first.World.Actors[0].Id).DefinitionGuid;
            source.World.Identities = source.World.Actors.Select(a => new EntityIdentityWire
                { Id = a.Id, DefinitionGuid = actorGuid, PlacementKey = "" }).ToArray();
            source.World.Buildings = Array.Empty<BuildingWire>();
            source.World.Worksites = Array.Empty<WorksiteWire>();
            source.World.Projectiles = Enumerable.Range(1, 1024).Select(id => new ProjectileWire
            {
                ViewId = id, FromX = 1, FromY = 2, ToX = 3, ToY = 4, Age = 0.2, Duration = 1
            }).ToArray();
            source.Events = Enumerable.Range(1, SessionViewData.MaximumEvents).Select(id => PresentationWire.From(
                new PresentationEvent(id, 0, "banner", new string('横', 256), new string('幅', 256)))).ToArray();
            source.Remnants = Enumerable.Range(SessionViewData.MaximumEvents + 1, SessionViewData.MaximumRemnants).Select(id => PresentationWire.From(
                new PresentationEvent(id, 0, "effect", cue: new VisualCue("rubble", 100, layout.GroundY, ContentId: "barracks")))).ToArray();
            var maximum = source.Freeze();
            byte[] initialBytes = codec.Encode(first);
            byte[] maximumBytes = codec.Encode(maximum);
            Assert.That(codec.Decode(maximumBytes).World.Projectiles.Count, Is.EqualTo(1024));
            Assert.That(maximumBytes.Length, Is.LessThanOrEqualTo(ProjectionCodec.MaximumBytes));
            var report = new JObject
            {
                ["context"] = "Unity Editor codec only; no transport measurement", ["initialBytes"] = initialBytes.Length,
                ["maximumBytes"] = maximumBytes.Length, ["maximumEntities"] = 256, ["maximumProjectiles"] = 1024,
                ["actorNameCharacters"] = 256, ["payloadLimit"] = ProjectionCodec.MaximumBytes,
                ["maximumPresentationEvents"] = SessionViewData.MaximumEvents,
                ["maximumActiveRemnants"] = SessionViewData.MaximumRemnants,
                ["initialPayloadBytesPerSecondAt10HzThreeClients"] = initialBytes.Length * 30,
                ["maximumPayloadBytesPerSecondAt10HzThreeClients"] = maximumBytes.Length * 30
            };
            Directory.CreateDirectory("../artifacts/migration");
            File.WriteAllText("../artifacts/migration/projection-wire-measurement.json", report.ToString());
        }
    }
}
