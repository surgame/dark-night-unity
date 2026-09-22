using System;
using System.Linq;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using NUnit.Framework;
using UnityEditor;

namespace DarkNights.Tests
{
    /// <summary>飞船验收的真实 YYGC 会话夹具；所有步行和驾驶通过可信连接输入推进，不注入角色运行坐标。</summary>
    internal static class ShipScenario
    {
        internal static ObjectSession Create(UnifiedSessionScope scope)
        {
            var catalog = RuleScenario.Catalog();
            var layout = new LevelLayout(5120, RuleScenario.Layout().GroundY, 380, 770, 900, 568,
                new[] { new PlacementDefinition("ship", 568) }, Array.Empty<PlacementDefinition>(), Array.Empty<PlacementDefinition>(),
                randomTerrain: true, expedition: true);
            var map = ExpeditionTerrainGenerator.Generate("SHIP-0922", "9765fd14785b4b0bb4ab7d7b7286b384");
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(Editor.Terrain.CaveTerrainAssets.DefinitionPath);
            return scope.NewWorld(catalog, layout, false, terrain: w => new SessionTerrain(w.Context, definition.LoadGameplayCatalog(), map));
        }
        internal static SessionConnection Connect(SessionAuthority authority, int slot)
        {
            var connection = authority.Connect(slot);
            Assert.That(authority.AcknowledgeReady(connection, authority.Epoch, authority.Revision, true), Is.True);
            return connection;
        }
        internal static ActorBehaviour Hero(ObjectSession world, int slot) => world.Index.Actors.Single(a => a.CaptureState().OwnerSlot == slot);
        internal static BuildingBehaviour Ship(ObjectSession world) => world.Index.Buildings.Single(b => b.RuleKey == "ship");
        internal static SessionResultCode Send(SessionAuthority authority, SessionConnection connection, long sequence, string command, ActorBehaviour hero = null, int? lease = null)
        {
            var request = new SessionRequest(SessionOperation.Expedition, SessionAuthority.ProtocolVersion, authority.Epoch,
                authority.PolicyRevision, sequence, hero == null ? null : new[] { hero.Id }, kind: command, controlLease: lease ?? hero?.CaptureState().ControlLease ?? 0);
            authority.Submit(connection, request);
            return authority.Tick().Single(r => r.Sequence == sequence).Code;
        }
        internal static void Input(SessionAuthority authority, SessionConnection connection, ActorBehaviour hero, int horizontal = 0, bool up = false, bool down = false)
        {
            var s = hero.CaptureState();
            Assert.That(authority.SubmitInput(connection, new HeroInputRequest(SessionAuthority.ProtocolVersion, authority.Epoch,
                authority.PolicyRevision, hero.Id, s.ControlLease, authority.ServerTick + 1, authority.ServerTick,
                horizontal, up, false, false, down)), Is.True);
            authority.Tick();
        }
        internal static void Walk(SessionAuthority authority, SessionConnection connection, ActorBehaviour hero, float x)
        {
            for (int i = 0; i < 1800 && Math.Abs(hero.X - x) > 1; i++) Input(authority, connection, hero, Math.Sign(x - hero.X));
            Input(authority, connection, hero);
            Assert.That(hero.X, Is.EqualTo(x).Within(1), "实际步行未到目标，不能用登船瞬移代替。");
        }
    }
}
