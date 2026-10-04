using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Definition;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>快速局的真实 YYGC 初始化与恢复回归；验证实际挥镐改格、重开基线和正常太空开局不受影响。</summary>
    public sealed class QuickTestSessionTests
    {
        [UnityTest]
        public IEnumerator LandedHeroMinesWithRealInputAndRestartRestoresCleanBaseline() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = await Create(scope);
            using var authority = new SessionAuthority(world);
            Assert.That(world.Journey.Capture().Phase, Is.EqualTo(JourneyPhase.Landed));
            Assert.That(world.CaptureView().Expedition.Ship.Phase, Is.Zero);
            var host = ShipScenario.Connect(authority, 0);
            var hero = ShipScenario.Hero(world, 0);
            int count = world.Index.Actors.Count();
            Assert.That(authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true), Is.True);
            Assert.That(world.Index.Actors.Count(), Is.EqualTo(count));
            Assert.That(hero.CaptureState().Boarded, Is.False);
            var state = hero.CaptureState();
            var tool = world.Resources.Equipment.Mining(state.Slot0);
            Assert.That(tool, Is.Not.Null); Assert.That(state.SelectedItem, Is.Zero);
            var map = world.Terrain.Map;
            Assert.That(TerrainMiningQuery.FirstSurface(map, state.X, state.Height + tool.HandHeight, 0, -1,
                tool.Reach, out CellCoord target, out _), Is.True);
            uint tile = map.Read(target).Cell.TileId;
            for (int i = 0; i < 600 && !map.Read(target).Cell.IsEmpty; i++)
            {
                state = hero.CaptureState(); var cell = map.Read(target).Cell;
                var mining = new HeroMiningTarget(map.World.WorldId.ToString(), map.World.Epoch, target.U, target.V,
                    cell.TileId, cell.Flags, contentVersion: map.ContentVersion(target));
                var input = new HeroInputRequest(SessionAuthority.ProtocolVersion, authority.Epoch, authority.PolicyRevision,
                    hero.Id, state.ControlLease, authority.ServerTick + 1, authority.ServerTick, 0, false, true, false, false,
                    aimAngle: -90, selectionRevision: state.SelectionRevision, mining: mining);
                Assert.That(authority.SubmitInput(host, input), Is.True); authority.Tick();
            }
            Assert.That(map.Read(target).Cell.IsEmpty, Is.True, "必须经过真实装备动作与落镐伤害改格。");
            string saved = world.SaveCodec.Serialize(world.CaptureWorld());
            world.Restore(saved);
            Assert.That(world.Terrain.Map.Read(target).Cell.IsEmpty, Is.True);
            world.Restart();
            Assert.That(world.Journey.Capture().Phase, Is.EqualTo(JourneyPhase.Landed));
            Assert.That(world.Terrain.Map.Read(target).Cell.TileId, Is.EqualTo(tile));
            var reset = ShipScenario.Hero(world, 0).CaptureState();
            Assert.That(reset.Boarded, Is.False); Assert.That(reset.CargoIron + reset.CargoGold, Is.Zero);
            Assert.That(reset.Slot0, Is.EqualTo(state.Slot0)); Assert.That(reset.ControllerSlot, Is.EqualTo(-1));
        });

        [UnityTest]
        public IEnumerator QuickPresetDoesNotChangeNextNormalOrbitWorld() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var quick = await Create(scope);
            using (var authority = new SessionAuthority(quick))
            {
                ShipScenario.Connect(authority, 0);
                Assert.That(quick.Journey.Capture().Phase, Is.EqualTo(JourneyPhase.Landed));
            }
            var normal = JourneyScenario.Create(scope);
            using var next = new SessionAuthority(normal);
            var host = ShipScenario.Connect(next, 0);
            Assert.That(normal.Journey.Capture().Phase, Is.EqualTo(JourneyPhase.Orbit));
            Assert.That(ShipScenario.Hero(normal, host.PlayerSlot).CaptureState().Boarded, Is.True);
        });

        [UnityTest]
        public IEnumerator EmbeddedMineralFixtureDepletesOneRealCellWithoutChangingForeground() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = await Create(scope, QuickTestPreset.EmbeddedMineralsId);
            using var authority = new SessionAuthority(world); var host = ShipScenario.Connect(authority, 0);
            var hero = ShipScenario.Hero(world, 0); var state = hero.CaptureState();
            var tool = world.Resources.Equipment.Mining(state.Slot0); var map = world.Terrain.Map;
            float radians = state.AimAngle * (float)Math.PI / 180;
            float dx = (float)Math.Cos(radians), dh = (float)Math.Sin(radians);
            var minerals = world.Terrain.Minerals;
            Assert.That(MineralMiningQuery.First(minerals, map, state.X, state.Height + tool.HandHeight, dx, dh,
                tool.Reach, out var target, out _), Is.True);
            int entities = world.Index.Count;
            for (int slot = 1; slot < 4; slot++)
            {
                ShipScenario.Connect(authority, slot);
                var peer = ShipScenario.Hero(world, slot).CaptureState();
                Assert.That(peer.X, Is.EqualTo(state.X)); Assert.That(peer.Height, Is.EqualTo(state.Height));
                Assert.That(peer.Slot0, Is.EqualTo(state.Slot0));
            }
            Assert.That(world.Index.Count, Is.EqualTo(entities), "快速局预设的四个专属角色直接接管，不重复增员。");
            var foreground = map.Read(target).Cell;
            int capacity = minerals.Query(target).State.RemainingReserves;
            for (int tick = 0; tick < 3000 && !minerals.Read(target).Cell.IsEmpty; tick++)
            {
                state = hero.CaptureState();
                var mining = new HeroMiningTarget(map.World.WorldId.ToString().Replace("-", ""), map.World.Epoch,
                    target.U, target.V, foreground.TileId, foreground.Flags, HeroMiningTargetKind.MineralDeposit,
                    0, map.ContentVersion(target), minerals.ContentVersion(target));
                var input = new HeroInputRequest(SessionAuthority.ProtocolVersion, authority.Epoch, authority.PolicyRevision,
                    hero.Id, state.ControlLease, authority.ServerTick + 1, authority.ServerTick, 0, false, true, false, false,
                    aimAngle: state.AimAngle, selectionRevision: state.SelectionRevision, mining: mining);
                Assert.That(authority.SubmitInput(host, input), Is.True); authority.Tick();
            }
            Assert.That(minerals.Read(target).Cell.IsEmpty, Is.True);
            Assert.That(world.Index.MineralDeposits, Is.Empty);
            Assert.That(map.Read(target).Cell, Is.EqualTo(foreground), "矿格耗尽不能修改前景");
            Assert.That(hero.CaptureState().CargoIron + hero.CaptureState().CargoGold, Is.EqualTo(capacity));
            Assert.That(minerals.Query(target).HasState, Is.False);
        });

        [UnityTest]
        public IEnumerator ConcurrentLastUnitAndRepeatedInputProduceOnlyOneCargoUnit() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = await Create(scope, QuickTestPreset.EmbeddedMineralsId);
            using var authority = new SessionAuthority(world);
            var connections = new[] { ShipScenario.Connect(authority, 0), ShipScenario.Connect(authority, 1) };
            var first = ShipScenario.Hero(world, 0).CaptureState();
            var tool = world.Resources.Equipment.Mining(first.Slot0); var map = world.Terrain.Map; var minerals = world.Terrain.Minerals;
            float angle = first.AimAngle * (float)Math.PI / 180;
            Assert.That(MineralMiningQuery.First(minerals, map, first.X, first.Height + tool.HandHeight,
                (float)Math.Cos(angle), (float)Math.Sin(angle), tool.Reach, out var target, out _), Is.True);
            var hit = typeof(MineralMapAuthority).GetMethod("StageHit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            world.Mutations.Run(() =>
            {
                int reserves = minerals.Query(target).State.RemainingReserves;
                for (int i = 1; i < reserves; i++) hit.Invoke(minerals, new object[] { target, 40, 0 });
                return true;
            });
            var foreground = map.Read(target).Cell;
            var intent = new HeroMiningTarget(map.World.WorldId.ToString().Replace("-", ""), map.World.Epoch,
                target.U, target.V, foreground.TileId, foreground.Flags, HeroMiningTargetKind.MineralDeposit,
                0, map.ContentVersion(target), minerals.ContentVersion(target));
            for (int tick = 0; tick < 300 && !minerals.Read(target).Cell.IsEmpty; tick++)
            {
                foreach (var connection in connections)
                {
                    var hero = ShipScenario.Hero(world, connection.PlayerSlot); var state = hero.CaptureState();
                    var input = new HeroInputRequest(SessionAuthority.ProtocolVersion, authority.Epoch, authority.PolicyRevision,
                        hero.Id, state.ControlLease, authority.ServerTick + 1, authority.ServerTick, 0, false, true, false, false,
                        aimAngle: first.AimAngle, selectionRevision: state.SelectionRevision, mining: intent);
                    Assert.That(authority.SubmitInput(connection, input), Is.True);
                    Assert.That(authority.SubmitInput(connection, input), Is.False, "重发同一输入序号必须拒绝。");
                }
                authority.Tick();
            }
            Assert.That(minerals.Read(target).Cell.IsEmpty, Is.True);
            int cargo = connections.Sum(connection =>
            { var state = ShipScenario.Hero(world, connection.PlayerSlot).CaptureState(); return state.CargoIron + state.CargoGold; });
            Assert.That(cargo, Is.EqualTo(1)); Assert.That(map.Read(target).Cell, Is.EqualTo(foreground));
        });

        private static async UniTask<ObjectSession> Create(UnifiedSessionScope scope, string id = QuickTestPreset.LandedPickaxeId)
        {
            var source = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch");
            var flow = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            var preset = QuickTestPreset.Create(id, flow);
            var cave = flow.FreezeCaveMap(); var modifiers = flow.FreezeModifiers();
            var map = await Task.Run(() => PlanetTerrainGenerator.GenerateCandidate(preset.Planet, preset.Seed,
                Guid.NewGuid().ToString("N"), cave, pipeline: modifiers));
            var catalog = RuleScenario.Catalog();
            var layout = new LevelLayout(5120, RuleScenario.Layout().GroundY, 380, 770, 900, 568,
                new[] { new PlacementDefinition("ship", 568) }, Array.Empty<PlacementDefinition>(), Array.Empty<PlacementDefinition>(),
                randomTerrain: true, expedition: true);
            var profile = TerrainProfileConfig.Resolve();
            ARDMapDefinition definition = profile.ContourDefinition;
            var rules = profile.Freeze(definition);
            return scope.NewWorld(catalog, layout, false, terrain: world =>
                new SessionTerrain(world.Context, rules.Business.Gameplay, map, rules, definition), quickTest: preset);
        }
    }
}
