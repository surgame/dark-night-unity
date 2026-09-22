using DarkNights.Core.ViewData;
using Newtonsoft.Json.Linq;
using static DarkNights.Runtime.Save.SaveJsonFields;

namespace DarkNights.Runtime.Save
{
    /// <summary>飞船显式保存边界；要求全部字段存在，恢复由权威对象撤销旧驾驶租约，不继承输入。</summary>
    internal static class ExpeditionShipJson
    {
        internal static JToken Write(ExpeditionShipData s) => s == null ? JValue.CreateNull() : new JObject
        {
            ["Id"] = s.Id, ["Phase"] = s.Phase, ["PilotId"] = s.PilotId,
            ["VelocityX"] = s.VelocityX, ["VelocityY"] = s.VelocityY, ["DoorClock"] = s.DoorClock,
            ["DockX"] = s.DockX, ["DockHeight"] = s.DockHeight
        };
        internal static ExpeditionShipData Read(JToken value)
        {
            var s = Object(value);
            return new ExpeditionShipData(Integer(s["Id"]), Integer(s["Phase"]), Integer(s["PilotId"]),
                (float)Number(s["VelocityX"]), (float)Number(s["VelocityY"]), Number(s["DoorClock"]),
                (float)Number(s["DockX"]), (float)Number(s["DockHeight"]));
        }
    }
}
