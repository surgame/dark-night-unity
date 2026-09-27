using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static DarkNights.Tests.ShipScenario;
using static DarkNights.Tests.JourneyScenario;

namespace DarkNights.Tests
{
    /// <summary>航程冻结合同和原子恢复验收；损坏数据必须在替换当前对象、地图或后台候选之前拒绝。</summary>
    public sealed class JourneySaveTests
    {
        [UnityTest]
        public IEnumerator OrbitSaveAndProjectionRoundTripPreserveFrozenCatalog() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); Connect(authority, 0);
            string json = world.SaveCodec.Serialize(world.CaptureWorld());
            var saved = world.SaveCodec.Parse(json).Expedition.Journey;
            Assert.That(world.Flow.Accepts(saved), Is.True); Assert.That(saved.Phase, Is.EqualTo(JourneyPhase.Orbit));
            var codec = new ProjectionCodec(world.Catalog, world.Layout);
            var wire = codec.Decode(codec.Encode(authority.CaptureProjection())).World.Expedition.Journey;
            Assert.That(world.Flow.Accepts(wire), Is.True); Assert.That(wire.Planets.Count, Is.EqualTo(saved.Planets.Count));
            world.Restore(json);
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Orbit));
            Assert.That(Hero(world, 0).CaptureState().ControllerSlot, Is.EqualTo(-1));
            Assert.That(Ship(world).CaptureState().PilotId, Is.Zero);
        });

        [UnityTest]
        public IEnumerator TransientJourneyCannotBeSavedThroughEitherBoundary() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); Cockpit(authority, host, world);
            Execute(authority, host, Select(authority, world, 1, Hero(world, 0)));
            Assert.Throws<FormatException>(() => world.SaveCodec.Serialize(world.CaptureWorld()));
            var request = new SessionRequest(SessionOperation.Save, SessionAuthority.ProtocolVersion, authority.Epoch,
                authority.PolicyRevision, 2);
            Assert.That(Execute(authority, host, request), Is.EqualTo(SessionResultCode.Loading));
            Assert.That(authority.StorageRequest, Is.Null);
        });

        [UnityTest]
        public IEnumerator SourceCatalogTamperCannotCancelAnExistingGeneration() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); Cockpit(authority, host, world);
            var saved = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
            saved["world"]["expedition"]["Journey"]["Planets"][0]["DisplayName"] = "篡改的源目录";
            Execute(authority, host, Select(authority, world, 1, Hero(world, 0)));
            var map = world.Terrain.Map; var task = Generation(world); int pilot = Ship(world).CaptureState().PilotId;
            Assert.Throws<FormatException>(() => world.Restore(saved.ToString()));
            Assert.That(world.Terrain.Map, Is.SameAs(map)); Assert.That(Generation(world), Is.SameAs(task));
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Preparing)); Assert.That(Ship(world).CaptureState().PilotId, Is.EqualTo(pilot));
        });

        [UnityTest]
        public IEnumerator MissingOversizedAndInvalidJourneyFieldsRejectWithoutRetiringWorld() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); Connect(authority, 0);
            string json = world.SaveCodec.Serialize(world.CaptureWorld()); var map = world.Terrain.Map;
            var missing = JObject.Parse(json); ((JObject)missing["world"]["expedition"]).Remove("Journey");
            Assert.Throws<FormatException>(() => world.Restore(missing.ToString()));
            var nullJourney = JObject.Parse(json); nullJourney["world"]["expedition"]["Journey"] = JValue.CreateNull();
            Assert.Throws<FormatException>(() => world.Restore(nullJourney.ToString()));
            var oversized = JObject.Parse(json); var rows = (JArray)oversized["world"]["expedition"]["Journey"]["Planets"];
            while (rows.Count <= 32) rows.Add(rows[0].DeepClone());
            Assert.Throws<FormatException>(() => world.Restore(oversized.ToString()));
            var invalid = JObject.Parse(json); invalid["world"]["expedition"]["Journey"]["Revision"] = 0;
            Assert.Throws<FormatException>(() => world.Restore(invalid.ToString()));
            Assert.That(world.Terrain.Map, Is.SameAs(map)); Assert.That(world.Context.IsAlive, Is.True);
        });

        [UnityTest]
        public IEnumerator DescentSaveRestoresHoverAndRequiresFreshControlLease() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            await Descent(authority, world, host); var hero = Hero(world, 0);
            for (int i = 0; i < 20; i++) Input(authority, host, hero, up: true);
            string json = world.SaveCodec.Serialize(world.CaptureWorld()); float height = Ship(world).CaptureState().Height;
            int oldLease = hero.CaptureState().ControlLease;
            var load = new SessionRequest(SessionOperation.BeginLoad, SessionAuthority.ProtocolVersion, authority.Epoch,
                authority.PolicyRevision, 2);
            authority.Submit(host, load); var receipt = authority.Tick().Single(r => r.Sequence == 2);
            Assert.That(receipt.Code, Is.EqualTo(SessionResultCode.Applied));
            int epoch = authority.Epoch; authority.CompleteLoad(receipt, json);
            hero = Hero(world, 0);
            Assert.That(authority.Epoch, Is.GreaterThan(epoch)); Assert.That(host.Ready, Is.False);
            Assert.That(hero.CaptureState().ControllerSlot, Is.EqualTo(-1)); Assert.That(hero.CaptureState().JumpHeld, Is.False);
            Assert.That(Ship(world).CaptureState().PilotId, Is.Zero); Assert.That(Ship(world).CaptureState().ShipVelocityY, Is.Zero);
            Assert.That(Ship(world).CaptureState().Height, Is.EqualTo(height));
            Assert.That(authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true), Is.True);
            Assert.That(authority.SubmitInput(host, new HeroInputRequest(SessionAuthority.ProtocolVersion, epoch, authority.PolicyRevision,
                hero.Id, oldLease, 999, authority.ServerTick, 0, true, false, false, false)), Is.False);
            for (int i = 0; i < 45; i++) authority.Tick();
            Assert.That(Ship(world).CaptureState().Height, Is.EqualTo(height)); Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Descent));
        });

        [UnityTest]
        public IEnumerator PlanetMapIdentityAndDockMustMatchBeforeRestore() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            await Descent(authority, world, host); string json = world.SaveCodec.Serialize(world.CaptureWorld()); var map = world.Terrain.Map;
            var identity = JObject.Parse(json); identity["world"]["expedition"]["Journey"]["MapId"] = Guid.NewGuid().ToString("N");
            Assert.Throws<FormatException>(() => world.Restore(identity.ToString()));
            var dock = JObject.Parse(json); dock["world"]["expedition"]["Ship"]["DockX"] = 1024;
            Assert.Throws<FormatException>(() => world.Restore(dock.ToString()));
            var phase = JObject.Parse(json); phase["world"]["expedition"]["Journey"]["Phase"] = (int)JourneyPhase.ArrivalSync;
            Assert.Throws<FormatException>(() => world.Restore(phase.ToString())); Assert.That(world.Terrain.Map, Is.SameAs(map));
        });

        [UnityTest]
        public IEnumerator NotificationExceptionCannotRollBackCommittedSelectionOrSkipCleanup() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); Cockpit(authority, host, world);
            var method = typeof(ObjectMutationBatch).GetMethod("AfterCommit", BindingFlags.Instance | BindingFlags.NonPublic);
            int cleanup = 0; var hero = Hero(world, 0);
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: injected journey notification"));
            Assert.DoesNotThrow(() => world.Mutations.Run(() =>
            {
                int result = world.Flow.SelectDestination(hero, world.Flow.Planets.First(p => p.Enabled).Id, world.Journey.Capture().Revision);
                method.Invoke(world.Mutations, new object[] { (Action)(() => throw new InvalidOperationException("injected journey notification")) });
                method.Invoke(world.Mutations, new object[] { (Action)(() => cleanup++) });
                return result;
            }));
            Assert.That(cleanup, Is.EqualTo(1)); Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Preparing));
            Assert.That(Ship(world).CaptureState().PilotId, Is.EqualTo(hero.Id));
        });

        [UnityTest]
        public IEnumerator WireRejectsInvalidCatalogAndUnknownPhase() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); Connect(authority, 0);
            var wire = JourneyWire.From(world.Journey.Capture()); wire.Phase = 99;
            Assert.Throws<FormatException>(() => wire.Freeze());
            wire = JourneyWire.From(world.Journey.Capture()); wire.Planets[0].ArrivalHeight = float.NaN;
            Assert.Throws<FormatException>(() => wire.Freeze());
            wire = JourneyWire.From(world.Journey.Capture()); wire.Planets = new PlanetWire[33];
            Assert.Throws<FormatException>(() => wire.Freeze());
        });
    }
}
