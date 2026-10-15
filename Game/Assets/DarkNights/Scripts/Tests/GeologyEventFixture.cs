using System;
using System.Reflection;
using AnyRules.Next;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>地质事件的真实YYGC有限夹具；复用正式角色、矿镐和地图，只在内存构造独立样板，不访问用户文件。事件使用已保存原生Prefab，清理先退休事件再释放会话。</summary>
    internal sealed class GeologyEventFixture : IDisposable
    {
        private UnifiedSessionScope scope;
        private GameObject eventRoot;
        public GeologyLabSession Lab { get; private set; }
        public ObjectSession World => Lab.World;
        public GeologyEventBehaviour Event { get; private set; }
        public GeologyEventConfig Config => Event.Configuration;

        internal static async UniTask<GeologyEventFixture> Create(bool region = false)
        {
            var fixture = new GeologyEventFixture();
            try
            {
                fixture.scope = await UnifiedSessionScope.Create();
                string root = Editor.Terrain.GeologyLabAssets.Root;
                var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(root + (region ? "/RegionRockfall.asset" : "/SupportCollapse.asset"));
                var config = System.Linq.Enumerable.Single(System.Linq.Enumerable.OfType<GeologyEventConfig>(definition.SharedConfigs)).Freeze();
                var catalog = RuleScenario.Catalog();
                var layout = new LevelLayout(5120, 0, 0, 5120, 704, 704,
                    new[] { new PlacementDefinition("ship", 192) }, Array.Empty<PlacementDefinition>(), Array.Empty<PlacementDefinition>(),
                    randomTerrain: true, expedition: true);
                var world = fixture.scope.NewWorld(catalog, layout, terrain: candidate =>
                {
                    var profile = TerrainProfileConfig.Resolve(); var rules = profile.Freeze();
                    return new SessionTerrain(candidate.Context, rules.Business.Gameplay, Map(config), rules, profile.Definition);
                }, journeyEnabled: false);
                fixture.Lab = new GeologyLabSession(world, 704, -32);
                fixture.eventRoot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Editor.Terrain.GeologyLabAssets.PrefabPath));
                fixture.Event = fixture.Lab.BindEvent(fixture.eventRoot.GetComponent<GameCore.Objects.Runner.ObjectInstance>(), definition);
                return fixture;
            }
            catch { fixture.Dispose(); throw; }
        }

        internal static PlayableTerrain Map(GeologyEventConfig config = null)
        {
            var cells = new byte[320 * 192]; var protection = new bool[cells.Length];
            for (int x = 0; x < 320; x++)
            { cells[191 * 320 + x] = 8; cells[42 * 320 + x] = 8; protection[42 * 320 + x] = true; }
            config = config ?? new GeologyEventConfig();
            cells[-config.SupportV * 320 + config.SupportU] = 2;
            for (int i = 0; i < config.RockCount; i++) { var cell = config.Source(i); cells[-cell.V * 320 + cell.U] = 2; }
            return new PlayableTerrain(Guid.NewGuid().ToString("N"), "CL02-REGRESSION", cells, protection,
                new bool[cells.Length], Array.Empty<TerrainRoom>(), Array.Empty<TerrainDepositBlueprint>(), expedition: true);
        }

        internal void BreakSupport() => Damage(Config.Support, int.MaxValue);
        internal void Damage(CellCoord position, int amount) => World.Mutations.Run(() => (bool)
            typeof(TerrainMapAuthority).GetMethod("StageDamage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(World.Terrain.Map, new object[] { position, amount, false }));
        internal void Tick(int frames = 1)
        {
            for (int index = 0; index < frames; index++) World.Mutations.Run(() => { Event.Tick(1d / 60); return true; });
        }
        internal void Position(float x, bool manual = true)
        {
            var actor = World.Index.Find<ActorBehaviour>(Lab.Hero.Id);
            World.Mutations.Run(() =>
            {
                var state = (ActorState)typeof(SessionStateBehaviour<ActorState>).GetMethod("Edit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actor, null);
                typeof(ActorState).GetProperty("X").SetValue(state, x);
                typeof(ActorState).GetProperty("Height").SetValue(state, -32f);
                typeof(ActorState).GetProperty("ManualControl").SetValue(state, manual); return true;
            });
        }

        public void Dispose()
        {
            // UnifiedSessionScope拥有ObjectSession，Lab只在本夹具内先释放事件；两次Dispose的世界退休入口幂等。
            Lab?.Dispose(); Lab = null;
            if (eventRoot != null) UnityEngine.Object.DestroyImmediate(eventRoot);
            scope?.Dispose(); scope = null;
        }
    }
}
