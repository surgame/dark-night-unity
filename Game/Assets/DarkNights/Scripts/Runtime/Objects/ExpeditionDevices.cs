using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>四设备部署、中继供能和撤收协调；生命周期与库存归各 BuildingState，搬运任务归机器人 ActorState。</summary>
    public sealed class ExpeditionDevices
    {
        private readonly ObjectSession world;
        private readonly ExpeditionDrone drone;
        internal ExpeditionNavigation Navigation { get; }
        private ExpeditionOperations Flight => world.Expedition;
        internal ExpeditionDevices(ObjectSession world) { this.world = world; Navigation = new ExpeditionNavigation(world); drone = new ExpeditionDrone(world); }
        internal void BeginDeployment()
        {
            if (!world.Ship.Docked) return;
            var camp = world.Camp.Read();
            if (camp.ResupplyCost == 0 && camp.RobotModule > 0 && !world.Index.Actors.Any(a => a.RuleKey == "hauler"))
            {
                var robot = world.Lifecycle.SpawnActor("hauler", Flight.Ship.X);
                robot.Edit().ExpeditionRole = 1; world.Ship.Cabin.Place(robot);
            }
            if (camp.ResupplyCost == 0 && camp.CrewModule > 0 && !world.Index.Actors.Any(a => a.RuleKey == "miner"))
            {
                var miner = world.Lifecycle.SpawnActor("miner", Flight.Ship.X + 80);
                miner.Edit().ExpeditionRole = 2; miner.Edit().Oxygen = Flight.Rules.OxygenSeconds; world.Ship.Cabin.Place(miner, 0);
            }
            if (camp.RobotModule == 0) return;
            if (camp.ResupplyCost == 0 && !world.Index.Actors.Any(a => a.RuleKey == "scout-drone"))
            {
                var scout = world.Lifecycle.SpawnActor("scout-drone", Flight.Ship.X + ShipGeometry.HatchX);
                scout.Edit().ExpeditionRole = 4; scout.Edit().Boarded = true; scout.Edit().Height = Flight.Ship.Read().Height + 84;
            }
            string[] kinds = camp.CrewModule > 0 ? new[] { "oxygen", "storage", "turret", "lamp", "oxygen" } :
                new[] { "oxygen", "storage", "turret", "lamp" };
            var used = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < kinds.Length; i++)
            {
                var device = world.Index.Buildings.FirstOrDefault(b => b.RuleKey == kinds[i] && !used.Contains(b.Id));
                if (device == null && camp.ResupplyCost > 0) continue;
                if (device == null) device = (BuildingBehaviour)world.Create(world.Resources.Find(kinds[i]), Flight.Ship.X, "", true, 0, "", null);
                used.Add(device.Id);
                var s = device.Edit();
                if (s.DeviceStage is 0 or 6) { s.X = Flight.Ship.X + ShipGeometry.HoldX; s.Height = Flight.Ship.Read().Height + ShipGeometry.HoldHeight; }
                s.DeviceStage = 1; s.ParentId = Flight.Ship.Id;
                s.TargetX = Flight.Ship.X - 130 + i * 80; s.TargetHeight = 0;
            }
        }
        internal bool OxygenAt(float x, float h) => world.Index.Buildings.Any(b => b.RuleKey == "oxygen" && b.Read().Powered &&
            Distance(x, h, b.X, b.Read().Height) <= Flight.Rules.OxygenRadius &&
            ExpeditionNavigation.Sight(world.Terrain.Map, b.X, b.Read().Height + 12, x, h + 12));

        internal int RequestRelay(ActorBehaviour hero)
        {
            var device = world.Index.Buildings.Where(b => b.RuleKey == "oxygen" && b.Read().DeviceStage is 0 or 3 or 6)
                .OrderByDescending(b => b.Id).FirstOrDefault();
            if (device == null || !world.Index.Actors.Any(a => a.RuleKey == "hauler")) return 0;
            var s = hero.Read();
            var parent = world.Index.Buildings.Where(b => b.Id < device.Id && (b == Flight.Ship || b.RuleKey == "oxygen" && b.Read().ParentId == Flight.Ship.Id) &&
                b.Read().Powered && Distance(s.X, s.Height, b.X, b.Read().Height) <= Flight.Rules.RelayRange)
                .OrderBy(b => Distance(s.X, s.Height, b.X, b.Read().Height)).FirstOrDefault();
            if (parent == null ||
                !Navigation.CanReach(world.Index.Actors.First(a => a.RuleKey == "hauler"), s.X, s.Height, false))
            { world.Notify("中继超出父节点范围或搬运路径未打通。", true); return 0; }
            var d = device.Edit(); d.TargetX = s.X; d.TargetHeight = s.Height; d.DeviceStage = 1; d.Powered = false; d.ParentId = parent.Id;
            return 1;
        }
        internal int AssignMiner(int id)
        {
            var deposit = world.Index.Find<MineralDepositBehaviour>(id);
            var miner = world.Index.Actors.FirstOrDefault(a => a.RuleKey == "miner");
            if (deposit == null || miner == null || deposit.Remaining <= 0) return 0;
            float height = PlayableTerrain.OriginY - (deposit.Y + .5f) * 16;
            if (!Navigation.CanReach(miner, deposit.X, height, false))
            { world.Notify("矿工需要双向步行通路，请先开路。", true); return 0; }
            miner.Edit().TaskTarget = id; miner.Edit().TaskPhase = 1; return 1;
        }
        internal void Tick(double delta)
        {
            int power = Flight.Rules.PowerSupply;
            foreach (var b in world.Index.Buildings.OrderBy(b => b.Id))
            {
                if (b == Flight.Ship) continue;
                var s = b.Edit(); var parent = world.Index.Find<BuildingBehaviour>(s.ParentId);
                int cost = b.RuleKey == "turret" ? 4 : b.RuleKey == "oxygen" ? 3 : 1;
                s.Powered = s.DeviceStage == 3 && parent != null && parent.Read().Powered && power >= cost &&
                    Distance(b.X, s.Height, parent.X, parent.Read().Height) <= Flight.Rules.RelayRange;
                if (s.Powered) power -= cost;
                if (world.Ship.Recalling && s.DeviceStage is 1 or 3) s.DeviceStage = 4;
            }
            foreach (var robot in world.Index.Actors.Where(a => a.RuleKey == "hauler").ToArray()) Robot(robot, delta);
            foreach (var miner in world.Index.Actors.Where(a => a.RuleKey == "miner").ToArray()) Miner(miner, delta);
            foreach (var scout in world.Index.Actors.Where(a => a.RuleKey == "scout-drone").ToArray()) drone.Tick(scout, delta);
        }
        private void Robot(ActorBehaviour robot, double delta)
        {
            var s = robot.Edit();
            s.Walking = false; s.ActionTime += delta;
            if (Flight.Ship.Read().ShipPhase >= 2 || robot.Hp <= 0) return;
            var device = world.Index.Find<BuildingBehaviour>(s.TaskTarget);
            if (device == Flight.Ship) device = null;
            if (device == null)
            {
                device = world.Index.Buildings.Where(b => b != Flight.Ship && b.Read().DeviceStage is 1 or 4)
                    .OrderBy(b => b.RuleKey == "oxygen" ? 1 : 0).ThenByDescending(b => b.Id).FirstOrDefault();
                if (device != null) { s.TaskTarget = device.Id; s.TaskPhase = 1; s.TaskClock = 0; }
            }
            if (device != null)
            {
                var d = device.Edit(); bool recall = world.Ship.Recalling;
                float x = s.TaskPhase == 2 ? (recall ? Flight.Ship.X + ShipGeometry.HoldX : d.TargetX) : d.X;
                float h = s.TaskPhase == 2 ? (recall ? Flight.Ship.Read().Height + ShipGeometry.HoldHeight : d.TargetHeight) : d.Height;
                if (s.TaskPhase == 2) { d.X = s.X; d.Height = s.Height; d.DeviceStage = 2; }
                if (!Navigation.Move(robot, x, h, delta, false)) return;
                if (s.TaskPhase != 2) { s.TaskPhase = 2; d.Powered = false; return; }
                s.TaskClock += delta;
                if (s.TaskClock < Flight.Rules.DeploySeconds) return;
                d.DeviceStage = recall ? 6 : 3; d.X = recall ? Flight.Ship.X + ShipGeometry.HoldX : d.TargetX; d.Height = recall ? Flight.Ship.Read().Height + ShipGeometry.HoldHeight : d.TargetHeight;
                s.TaskTarget = s.TaskPhase = 0; s.TaskClock = 0; return;
            }
            var storage = world.Index.Buildings.FirstOrDefault(b => b.RuleKey == "storage" && b.Read().DeviceStage == 3 && b.Read().CargoIron + b.Read().CargoGold > 0);
            bool carrying = s.CargoIron + s.CargoGold > 0;
            if (carrying || storage == null || world.Ship.Recalling)
            {
                if (!Navigation.Move(robot, Flight.Ship.X + ShipGeometry.HoldX, Flight.Ship.Read().Height + ShipGeometry.HoldHeight, delta, false)) return;
                ExpeditionCargo.Transfer(robot, Flight.Ship, Flight.Rules.ShipCapacity * (1 + world.Camp.Read().CargoModule));
                if (world.Ship.Recalling) s.Boarded = true;
            }
            else if (Navigation.Move(robot, storage.X, storage.Read().Height, delta, false))
                ExpeditionCargo.Load(storage, robot, Flight.Rules.BagCapacity);
        }
        private void Miner(ActorBehaviour miner, double delta)
        {
            var s = miner.Edit(); s.Walking = false; s.ActionTime += delta;
            if (Flight.Ship.Read().ShipPhase >= 2 || miner.Hp <= 0) return;
            bool returning = world.Ship.Recalling || s.Oxygen < 25 || s.CargoIron + s.CargoGold >= Flight.Rules.BagCapacity;
            var deposit = world.Index.Find<MineralDepositBehaviour>(s.TaskTarget);
            if (returning || deposit == null || deposit.Remaining == 0)
            {
                var destination = world.Index.Buildings.FirstOrDefault(b => b.RuleKey == "storage" && b.Read().Powered &&
                    b.Read().CargoIron + b.Read().CargoGold < Flight.Rules.StorageCapacity) ?? Flight.Ship;
                if (world.Ship.Recalling || s.Oxygen < 25) destination = Flight.Ship;
                if (!Navigation.Move(miner, destination.X + (destination == Flight.Ship ? ShipGeometry.HoldX : 0), destination.Read().Height + (destination == Flight.Ship ? ShipGeometry.HoldHeight : 0), delta, false)) return;
                ExpeditionCargo.Transfer(miner, destination, destination == Flight.Ship ? Flight.Rules.ShipCapacity * (1 + world.Camp.Read().CargoModule) : Flight.Rules.StorageCapacity);
                if (world.Ship.Recalling) s.Boarded = true;
                return;
            }
            float h = PlayableTerrain.OriginY - (deposit.Y + .5f) * 16;
            if (!Navigation.Move(miner, deposit.X, h, delta, false)) return;
            if (!ExpeditionNavigation.Sight(world.Terrain.Map, s.X, s.Height + 9, deposit.X, h)) return;
            s.TaskClock += delta;
            if (s.TaskClock < Flight.Rules.ExtractSeconds || !ExpeditionCargo.CanMine(miner)) return;
            s.TaskClock = 0;
            if (deposit.ExtractByHand()) ExpeditionCargo.Collect(miner, deposit.ResourceId);
        }
        internal static double Distance(float ax, float ay, float bx, float by) => Math.Sqrt(Math.Pow(ax - bx, 2) + Math.Pow(ay - by, 2));
    }
}
