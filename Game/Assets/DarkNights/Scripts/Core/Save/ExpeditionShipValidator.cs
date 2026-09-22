using System;
using System.Linq;
using DarkNights.Core.ViewData;

namespace DarkNights.Core.Save
{
    /// <summary>校验船体冻结字段和驾驶席引用；数值非有限、阶段不匹配及舱外驾驶者不能进入网络或存档。</summary>
    public static class ExpeditionShipValidator
    {
        public static bool Transform(ExpeditionShipData s, int id, float x, float height, Config.ShipFlightDefinition rules)
        {
            if (s == null || s.Id != id || s.DockX != Logic.Terrain.ExpeditionTerrainGenerator.ShipX || s.DockHeight != 0 ||
                float.IsNaN(x) || float.IsNaN(height) || Math.Abs(x - s.DockX) > rules.HorizontalRange ||
                height < 0 || height > rules.MaximumLift) return false;
            return s.Phase == 3 || Math.Abs(x - s.DockX) < .001f && Math.Abs(height) < .001f;
        }
        public static string Validate(ExpeditionViewData data, Config.ShipFlightDefinition rules)
        {
            var s = data.Ship;
            bool Finite(double v, double low, double high) => !double.IsNaN(v) && !double.IsInfinity(v) && v >= low && v <= high;
            if (s == null || !data.Devices.Any(d => d.Id == s.Id) || s.Phase is < 0 or > 3 ||
                !Finite(s.VelocityX, -rules.HorizontalSpeed, rules.HorizontalSpeed) || !Finite(s.VelocityY, -rules.VerticalSpeed, rules.VerticalSpeed) || !Finite(s.DoorClock, 0, rules.DoorSeconds) ||
                !Finite(s.DockX, 176, 4944) || !Finite(s.DockHeight, -2400, 128)) return "飞船运动合同无效";
            if (s.Phase != 3 && (s.VelocityX != 0 || s.VelocityY != 0) || s.Phase != 2 && s.DoorClock != 0)
                return "飞船阶段与运动不一致";
            if (s.PilotId != 0 && !data.Crew.Any(a => a.Id == s.PilotId && a.Role == 0 && a.Boarded && a.OwnerSlot >= 0))
                return "驾驶席引用无效";
            if (s.Phase is 1 or 2 && s.PilotId == 0) return "收舱缺少驾驶者";
            return "";
        }
    }
}
