using System;
using System.Collections.Generic;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Objects
{
    /// <summary>独立功能场景的本地权威调度器；复用正式ObjectSession、角色运动及挥镐事务。只持有对象引用，不开放网络、文件保存或通用调试命令，事件在相同60Hz事务内推进。</summary>
    public sealed class GeologyLabSession : IDisposable
    {
        private readonly List<ObjectInstance> eventOwners = new List<ObjectInstance>();
        private readonly List<GeologyEventBehaviour> events = new List<GeologyEventBehaviour>();
        private ActorBehaviour hero;
        public ObjectSession World { get; }
        public ActorViewData Hero => hero == null ? null : ObjectProjection.CaptureActor(World, hero);
        public ObjectInstance HeroObject => hero?.Object;
        public IReadOnlyList<GeologyEventBehaviour> Events => events.AsReadOnly();
        public bool Paused => World.Paused;
        public double SimulationTime => World.Camp.Read().Elapsed;

        public GeologyLabSession(ObjectSession world, float spawnX, float spawnHeight)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            if (!world.Context.IsActive || world.Terrain?.Map == null || float.IsNaN(spawnHeight) || float.IsInfinity(spawnHeight) ||
                TerrainBodyCollision.Blocked(world.Terrain.Map, spawnX, spawnHeight,
                    HeroControlDefinition.BodyHalfWidth, HeroControlDefinition.BodyHeight) ||
                !TerrainBodyCollision.Ground(world.Terrain.Map, spawnX, spawnHeight + .01f, spawnHeight - .01f,
                    HeroControlDefinition.BodyHalfWidth, HeroControlDefinition.BodyHeight, out _))
                throw new InvalidOperationException("功能场景出生点缺少权威地图或身体净空。");
            world.Mutations.Run(() =>
            {
                hero = world.Lifecycle.SpawnActor("worker", spawnX, name: "地质测试员");
                var state = hero.Edit();
                state.ManualControl = true; state.ControllerSlot = 0; state.ControlLease = 1;
                state.OwnerSlot = 0; state.Height = spawnHeight; state.SupportPlatform = 0;
                var pickaxe = world.Resources.Equipment.MiningDefinitions.First();
                if (!HeroInventoryBehaviour.Give(state, pickaxe.Guid.ToString())) throw new InvalidOperationException("功能场景矿镐配发失败。");
                state.SelectedItem = 0; return true;
            });
        }

        public GeologyEventBehaviour BindEvent(ObjectInstance instance, ObjectDefinition definition)
        {
            if (instance == null || definition == null || eventOwners.Contains(instance) || events.Count >= 8)
                throw new InvalidOperationException("功能场景事件绑定无效或超过八个。");
            instance.Initialize("geology-lab:" + definition.Key, definition, session: World.Context, activate: false);
            eventOwners.Add(instance);
            var behaviour = instance.GetBehaviour<GeologyEventBehaviour>() ?? throw new InvalidOperationException("事件缺少地质能力。");
            behaviour.Prepare(); instance.Activate(); events.Add(behaviour); return behaviour;
        }

        public void SetPaused(bool value) => World.SetTime(value, 1);

        public void Step(int horizontal, bool jumpPressed, bool jumpHeld, bool usePressed, bool useHeld, float aim, bool sprint = false)
        {
            if (World.Paused) return;
            if (horizontal < -1 || horizontal > 1 || float.IsNaN(aim) || float.IsInfinity(aim) || Math.Abs(aim) > 180)
                throw new ArgumentException("功能场景输入无效。");
            World.Mutations.Run(() =>
            {
                var state = hero.Edit();
                state.Horizontal = horizontal; state.JumpPending |= jumpPressed; state.JumpHeld = jumpHeld;
                state.SprintHeld = sprint; state.UsePressed |= usePressed; state.UseHeld = useHeld; state.AimAngle = aim;
                SetMiningTarget(state, aim);
                double delta = World.Camp.BeginStep(1d / 60);
                hero.Tick(delta);
                foreach (var geology in events) geology.Tick(delta);
                return true;
            });
        }

        private void SetMiningTarget(ActorState state, float aim)
        {
            state.MiningWorldId = null; state.MiningTargetKind = HeroMiningTargetKind.None;
            var tool = hero.Object.GetBehaviour<HeroInventoryBehaviour>().MiningTool()?.Rules;
            if (tool == null) return;
            var map = World.Terrain.Map;
            if (state.PickaxeSwingActive && events.Any(e => e.ReservesSource(new CellCoord(state.PickaxeSwingTarget.U, state.PickaxeSwingTarget.V))))
                HeroEquipment.Cancel(state);
            float radians = aim * (float)Math.PI / 180;
            if (!TerrainMiningQuery.FirstSurface(map, state.X, state.Height + tool.HandHeight,
                (float)Math.Cos(radians), (float)Math.Sin(radians), tool.Reach, out var target, out _) ||
                !map.Read(target).TryGetCell(out var cell)) return;
            if (events.Any(e => e.ReservesSource(target))) return;
            state.MiningWorldId = map.World.WorldId.ToString().Replace("-", ""); state.MiningMapEpoch = map.World.Epoch;
            state.MiningU = target.U; state.MiningV = target.V; state.MiningTileId = cell.TileId; state.MiningFlags = cell.Flags;
            state.MiningContentVersion = map.ContentVersion(target); state.MiningMineralContentVersion = 0;
            state.MiningTargetKind = HeroMiningTargetKind.Foreground; state.MiningEntityId = 0;
        }

        public void Dispose()
        {
            foreach (var instance in eventOwners) instance.Release();
            eventOwners.Clear(); events.Clear(); hero = null;
            World.Dispose();
        }
    }
}
