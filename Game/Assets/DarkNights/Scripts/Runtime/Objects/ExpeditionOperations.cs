using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 地面会话的主角与飞船调度入口；经济、NPC、警戒、设备及返航结算已退出运行。
    /// 保留规则和船体引用供购买及驾驶复用，持久状态仍归原有 YYGC Behaviour。
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

        internal void ShowIntro() => world.Feedback.ShowBanner("星球地面", "自由移动与跳跃；步行进入飞船可购买装备或操作驾驶台。");

        internal void Advance(double seconds)
        {
            world.Mutations.Run(() =>
            {
                double delta = world.Camp.BeginStep(seconds);
                world.Ship.Tick(delta);
                foreach (var actor in world.Index.Actors.Where(a => a.Read().ManualControl).ToArray())
                    actor.Tick(delta);
                return true;
            });
        }

        internal void Prepare()
        {
            var camp = world.Camp.Edit();
            camp.ExpeditionRun = 1;
            world.Economy.SetStock(new ResourceAmounts());
            var ship = Ship.Edit(); ship.DeviceStage = 3; ship.Powered = true;
            ship.DockX = ship.X; ship.DockHeight = ship.Height;
            if (!world.Flow.Enabled) return;
            // 空载体只供保留实现的隔离夹具；正式地图选择始终生成星球地面。
            if (world.Terrain.Seed == PlanetTerrainGenerator.SpaceSeed) world.Flow.PrepareOrbit();
        }

        internal int Command(string operation, int target, ActorBehaviour hero) =>
            operation is "pilot" or "takeoff" or "land" or "cancel-flight" ? world.Ship.Command(operation, hero) : 0;

        internal void BeginGround()
        {
            world.Camp.Edit().ExpeditionPhase = 0;
        }
    }
}
