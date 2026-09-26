using System;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Runner;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>仅用于独立洞穴工作台的 YYGC 权威会话；复用正式 ActorState 与运动算法测试格形状，不启动经济、战斗或玩家存档。</summary>
    public sealed class CaveWorkshopSession : IDisposable
    {
        private readonly DIContainer container;
        private readonly ObjectSessionContext context;
        private readonly HeroControlDefinition rules;
        private readonly float speed;
        private readonly ActorState actor;
        private bool active = true;
        private ulong sequence;
        public TerrainMapAuthority Map { get; }
        public WorkshopTerrainEdits Edits { get; }
        public float X => actor.X / PlayableTerrain.CellPixels;
        public float Y => (actor.Height - PlayableTerrain.OriginY) / PlayableTerrain.CellPixels;
        public double Fuel => actor.JetpackFuel;
        public bool Grounded => actor.SupportPlatform >= 0;
        public CaveWorkshopSession(TerrainBlueprint blueprint, ServerGameplayCatalog catalog, GameCatalog game)
        {
            var source = game.Balance.HeroControl;
            // 工作台全图原点在顶部；只把全局高度上限平移到地图顶，跳跃与燃料数值保持原配置。
            rules = new HeroControlDefinition(source.JumpSpeed, source.Gravity, PlayableTerrain.OriginY + 8,
                source.JetpackSpeed, source.FuelSeconds, source.FuelRecovery, source.DropSeconds, source.WorkReach);
            speed = (float)game.Balance.Units["worker"].Speed;
            container = new DIContainer(); container.Initialize();
            context = ObjectSessionContext.CreateAuthority(container, () => active); context.Activate();
            Map = new TerrainMapAuthority(context, blueprint, catalog, new WorldIdentity(StableGuid.Parse(Guid.NewGuid().ToString("N")), 1));
            Edits = new WorkshopTerrainEdits(Map, blueprint);
            actor = new ActorState { JetpackEquipped = true, JetpackFuel = rules.FuelSeconds, SupportPlatform = -1 };
            Teleport(blueprint.Rooms[0].X, -blueprint.Rooms[0].Y);
        }
        public void Teleport(float x, float y)
        {
            actor.X = x * PlayableTerrain.CellPixels;
            actor.Height = PlayableTerrain.OriginY + y * PlayableTerrain.CellPixels;
            actor.VerticalSpeed = 0; actor.SupportPlatform = -1; actor.JetpackFuel = rules.FuelSeconds;
        }
        public void Tick(float horizontal, bool jump, bool held, float seconds)
        {
            TerrainHeroMotion.MoveHorizontal(Map, actor, actor.X + horizontal * speed * seconds);
            TerrainHeroMotion.Tick(Map, actor, rules, seconds, jump, held);
        }
        public bool Edit(float x, float y, bool explode)
        {
            if ((x-X)*(x-X)+(y-Y)*(y-Y) > 36) return false;
            var center = new CellCoord((int)Math.Floor(x + .5), (int)Math.Floor(y + .5));
            var action = explode ? TerrainEditAction.Explosive : TerrainEditAction.HandMine;
            try
            {
                var targets = Map.BuildTargets(action, center);
                Map.DestroyTrusted(1, "workshop:" + (++sequence), action, Map.World, center, targets, _ => true);
                return true;
            }
            catch (InvalidOperationException) { return false; }
            catch (ArgumentException) { return false; }
        }
        public void Dispose()
        { active = false; Map.Dispose(); context.Dispose(); container.OnReturnToPool(); }
    }
}
