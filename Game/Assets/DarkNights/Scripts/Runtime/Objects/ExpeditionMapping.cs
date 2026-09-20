using System.Linq;
using DarkNights.Core.ViewData;
namespace DarkNights.Runtime.Objects
{
    /// <summary>把所属 YYGC 状态冻结为远征补充合同；恢复在现有世界替换事务内执行，不保存第二份运行状态。</summary>
    internal static class ExpeditionMapping
    {
        internal static ExpeditionViewData Capture(ObjectSession world)
        {
            if (!world.IsExpedition) return null;
            var c = world.Camp.Read();
            var actors = world.Index.Actors.Select(a =>
            {
                var s = a.Read();
                return new ExpeditionActorData(a.Id, s.Oxygen, s.CargoIron, s.CargoGold, s.ExpeditionRole, s.TaskTarget, s.TaskPhase, s.TaskClock, s.OwnerSlot, s.Boarded);
            }).ToArray();
            var devices = world.Index.Buildings.Select(a =>
            {
                var s = a.Read();
                return new ExpeditionDeviceData(a.Id, s.Height, s.CargoIron, s.CargoGold, s.DeviceStage, s.ParentId, s.TargetX, s.TargetHeight, s.Powered);
            }).ToArray();
            return new ExpeditionViewData(c.ExpeditionRun, c.ExpeditionPhase, c.ExpeditionRisk, c.ExpeditionClock, c.ExpeditionSettled, c.RobotModule, c.CargoModule, c.CrewModule, c.LostCargo, c.LostDevices, actors, devices, c.ResupplyCost);
        }
        internal static void Restore(ObjectSession world, ExpeditionViewData data)
        {
            if (data == null) return;
            var c = world.Camp.Edit();
            c.ExpeditionRun = data.Run;
            c.ExpeditionPhase = data.Phase;
            c.ExpeditionRisk = data.Risk;
            c.ExpeditionClock = data.Clock;
            c.ExpeditionSettled = data.Settled;
            c.RobotModule = data.RobotModule;
            c.CargoModule = data.CargoModule;
            c.CrewModule = data.CrewModule;
            c.LostCargo = data.LostCargo;
            c.LostDevices = data.LostDevices;
            c.ResupplyCost = data.ResupplyCost;
            foreach (var item in data.Crew)
            {
                var s = world.Index.Find<ActorBehaviour>(item.Id).Edit();
                s.Oxygen = item.Oxygen;
                s.CargoIron = item.Iron;
                s.CargoGold = item.Gold;
                s.ExpeditionRole = item.Role;
                s.TaskTarget = item.TaskTarget;
                s.TaskPhase = item.TaskPhase;
                s.TaskClock = item.TaskClock;
                s.OwnerSlot = item.OwnerSlot;
                s.Boarded = item.Boarded;
            }
            foreach (var item in data.Devices)
            {
                var s = world.Index.Find<BuildingBehaviour>(item.Id).Edit();
                s.Height = item.Height;
                s.CargoIron = item.Iron;
                s.CargoGold = item.Gold;
                s.DeviceStage = item.Stage;
                s.ParentId = item.ParentId;
                s.TargetX = item.TargetX;
                s.TargetHeight = item.TargetHeight;
                s.Powered = item.Powered;
            }
        }
    }
}
