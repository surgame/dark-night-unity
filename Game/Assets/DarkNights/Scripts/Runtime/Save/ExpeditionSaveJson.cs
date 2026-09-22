using System.Linq;
using DarkNights.Core.ViewData;
using Newtonsoft.Json.Linq;
using static DarkNights.Runtime.Save.SaveJsonFields;
namespace DarkNights.Runtime.Save
{
    /// <summary>远征补充合同的显式 JSON 边界；随世界原子写盘，拒绝缺失字段，结算标记与资源同档。</summary>
    internal static class ExpeditionSaveJson
    {
        internal static JToken Write(ExpeditionActorData d) => d == null ? JValue.CreateNull() : new JObject
        {
            ["Id"] = d.Id,
            ["Oxygen"] = d.Oxygen,
            ["Iron"] = d.Iron,
            ["Gold"] = d.Gold,
            ["Role"] = d.Role,
            ["TaskTarget"] = d.TaskTarget,
            ["TaskPhase"] = d.TaskPhase,
            ["TaskClock"] = d.TaskClock,
            ["OwnerSlot"] = d.OwnerSlot,
            ["Boarded"] = d.Boarded,
        };
        internal static ExpeditionActorData Actor(JToken t)
        {
            if (t?.Type == JTokenType.Null) return null;
            var d = Object(t);
            return new ExpeditionActorData(Integer(d["Id"]), Number(d["Oxygen"]), Integer(d["Iron"]), Integer(d["Gold"]), Integer(d["Role"]), Integer(d["TaskTarget"]), Integer(d["TaskPhase"]), Number(d["TaskClock"]), Integer(d["OwnerSlot"]), Boolean(d["Boarded"]));
        }
        internal static JToken Write(ExpeditionDeviceData d) => d == null ? JValue.CreateNull() : new JObject
        {
            ["Id"] = d.Id,
            ["Height"] = d.Height,
            ["Iron"] = d.Iron,
            ["Gold"] = d.Gold,
            ["Stage"] = d.Stage,
            ["ParentId"] = d.ParentId,
            ["TargetX"] = d.TargetX,
            ["TargetHeight"] = d.TargetHeight,
            ["Powered"] = d.Powered,
        };
        internal static ExpeditionDeviceData Device(JToken t)
        {
            if (t?.Type == JTokenType.Null) return null;
            var d = Object(t);
            return new ExpeditionDeviceData(Integer(d["Id"]), (float)Number(d["Height"]), Integer(d["Iron"]), Integer(d["Gold"]), Integer(d["Stage"]), Integer(d["ParentId"]), (float)Number(d["TargetX"]), (float)Number(d["TargetHeight"]), Boolean(d["Powered"]));
        }
        internal static JToken Write(ExpeditionViewData d) => d == null ? JValue.CreateNull() : new JObject
        {
            ["Ship"] = ExpeditionShipJson.Write(d.Ship),
            ["Run"] = d.Run,
            ["Phase"] = d.Phase,
            ["Risk"] = d.Risk,
            ["Clock"] = d.Clock,
            ["Settled"] = d.Settled,
            ["RobotModule"] = d.RobotModule,
            ["CargoModule"] = d.CargoModule,
            ["CrewModule"] = d.CrewModule,
            ["LostCargo"] = d.LostCargo,
            ["LostDevices"] = d.LostDevices,
            ["ResupplyCost"] = d.ResupplyCost,
            ["Crew"] = new JArray(d.Crew.Select(Write)), ["Devices"] = new JArray(d.Devices.Select(Write)),
        };
        internal static ExpeditionViewData Read(JToken t)
        {
            if (t?.Type == JTokenType.Null) return null;
            var d = Object(t);
            return new ExpeditionViewData(Integer(d["Run"]), Integer(d["Phase"]), Number(d["Risk"]), Number(d["Clock"]), Boolean(d["Settled"]), Integer(d["RobotModule"]), Integer(d["CargoModule"]), Integer(d["CrewModule"]), Integer(d["LostCargo"]), Integer(d["LostDevices"]), Array(d["Crew"], Actor, 256).ToArray(), Array(d["Devices"], Device, 256).ToArray(), Integer(d["ResupplyCost"]), ExpeditionShipJson.Read(d["Ship"]));
        }
    }
}
