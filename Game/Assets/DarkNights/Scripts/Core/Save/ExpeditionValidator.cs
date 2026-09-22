using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;

namespace DarkNights.Core.Save
{
    /// <summary>远征冻结合同的数值和跨对象关系验证；非法库存、阶段和重复玩家身份不能进入保存或副本。</summary>
    public static class ExpeditionValidator
    {
        public static string Validate(ExpeditionViewData data, int[] actors, int[] buildings, int[] deposits, ExpeditionDefinition rules)
        {
            if (data == null) return "远征合同缺失";
            bool Number(double v, double max) => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0 && v <= max;
            if (data.Run < 1 || data.Run > 1000000 || data.Phase < 0 || data.Phase > 4 ||
                !Number(data.Risk, 1000000) || !Number(data.Clock, 1000000) || data.Settled != (data.Phase == 4) ||
                data.RobotModule is < 0 or > 1 || data.CargoModule is < 0 or > 1 || data.CrewModule is < 0 or > 1 ||
                !Number(data.LostCargo, 1000000) || !Number(data.LostDevices, 256) || !Number(data.ResupplyCost, 1000000)) return "远征阶段或成长无效";
            if (data.Crew.Count != actors.Length || data.Devices.Count != buildings.Length ||
                data.Crew.Any(a => a == null || !actors.Contains(a.Id)) || data.Devices.Any(b => b == null || !buildings.Contains(b.Id)) ||
                data.Crew.Select(a => a.Id).Distinct().Count() != actors.Length ||
                data.Devices.Select(b => b.Id).Distinct().Count() != buildings.Length) return "远征对象集合不一致";
            if (data.Crew.Where(a => a.OwnerSlot >= 0).GroupBy(a => a.OwnerSlot).Any(g => g.Count() > 1)) return "远征玩家身份重复";
            foreach (var a in data.Crew)
                if (!Number(a.Oxygen, rules.OxygenSeconds) || !Number(a.Iron, rules.BagCapacity) ||
                    !Number(a.Gold, rules.BagCapacity - a.Iron) || a.Role is < 0 or > 4 || a.OwnerSlot is < -1 or > 3 ||
                    a.TaskPhase is < 0 or > 9 || !Number(a.TaskClock, 1000000) ||
                    a.TaskTarget != 0 && !buildings.Contains(a.TaskTarget) && !deposits.Contains(a.TaskTarget)) return "远征角色状态无效";
            foreach (var b in data.Devices)
                if (b.Stage is < 0 or > 6 || !Number(b.Iron, rules.ShipCapacity * 2) || !Number(b.Gold, rules.ShipCapacity * 2 - b.Iron) ||
                    !Number(b.Height + 2560, 2816) || !Number(b.TargetHeight + 2560, 2688) || !Number(b.TargetX, 5120) ||
                    b.ParentId != 0 && (!buildings.Contains(b.ParentId) || b.ParentId == b.Id)) return "远征设备状态无效";
            return ExpeditionShipValidator.Validate(data, rules.Ship);
        }
    }
}
