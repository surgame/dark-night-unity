using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 会话主角、投射物和固定飞船的调度入口；旧NPC、警戒、设备及返航系统不参与运行。
    /// 普通航程与交货调用原Journey、船体及Economy，持久状态仍归所属YYGC Behaviour。
    /// </summary>
    public sealed class ExpeditionOperations
    {
        private readonly ObjectSession world;
        /// <summary>历史验收夹具保留的回调槽；当前地面会话只通过显式保存入口写盘，不自动结算或调用此回调。</summary>
        public Action<DarkNights.Core.Save.SessionSnapshot> CommitSave { get; set; }
        internal ExpeditionOperations(ObjectSession world) { this.world = world; }
        internal ExpeditionDefinition Rules => world.Catalog.Balance.Expedition;
        internal BuildingBehaviour Ship => world.Index.Buildings.FirstOrDefault(b => b.RuleKey == "ship");
        internal bool Active => world.IsExpedition && (!world.Flow.Enabled || !world.Flow.IsSpace);
        internal bool IncludeInitial(ObjectPlacement placement) =>
            world.Terrain.Seed == PlanetTerrainGenerator.SpaceSeed || ObjectSessionResources.Rule(placement.Definition) != "ship";

        internal void ShowIntro() => world.Feedback.ShowBanner("星球地面",
            Ship == null ? "自由移动、跳跃与采集；飞船及其补给、交货和航程暂时停用。" :
            "此存档保留原飞船与船内服务。");

        internal void Advance(double seconds)
        {
            world.Mutations.Run(() =>
            {
                double delta = world.Camp.BeginStep(seconds);
                world.Flow.Tick(delta);
                world.Ship.Tick(delta);
                foreach (var actor in world.Index.Actors.Where(a => a.Read().ManualControl).ToArray())
                    actor.Tick(delta);
                world.Projectiles.Tick(delta);
                world.TerrainGameplay?.Tick(delta);
                world.Geology.Tick(delta);
                return true;
            });
        }

        internal void Prepare()
        {
            var camp = world.Camp.Edit();
            camp.ExpeditionRun = 1;
            world.Economy.SetStock(new ResourceAmounts());
            if (Ship == null) { world.Journey.Edit().Enabled = false; return; }
            var ship = Ship.Edit(); ship.DeviceStage = 3; ship.Powered = true;
            ship.DockX = ship.X; ship.DockHeight = ship.Height;
            if (!world.Flow.Enabled) return;
            // 空载体只供保留实现的隔离夹具；正式地图选择始终生成星球地面。
            if (world.Terrain.Seed == PlanetTerrainGenerator.SpaceSeed) world.Flow.PrepareOrbit();
        }

        internal int Command(string operation, int target, ActorBehaviour hero)
        {
            if (target != Ship?.Id) return 0;
            if (operation == "unload") return new ShipCargoService(world).Deposit(hero);
            if (operation == "depart") return world.Flow.Depart(hero);
            if (operation == "cancel-flight") return world.Flow.Cancel(hero);
            return world.Flow.Enabled ? 0 : world.Ship.Command(operation, hero);
        }

        internal void BeginGround()
        {
            world.Camp.Edit().ExpeditionPhase = 0;
        }
    }
}
