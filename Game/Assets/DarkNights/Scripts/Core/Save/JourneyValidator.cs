using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;

namespace DarkNights.Core.Save
{
    /// <summary>航程冻结数据的纯校验；存档限稳定阶段，网络允许等待阶段，但始终约束配置、世界身份和船舱关系。</summary>
    public static class JourneyValidator
    {
        public static string Validate(JourneyViewData d, bool stableOnly = false, string worldId = null)
        {
            if (d == null) return "";
            bool Text(string v, int max) => v != null && v.Length <= max;
            if (!d.Enabled || !Enum.IsDefined(typeof(JourneyPhase), d.Phase) || d.Revision < 1 ||
                !Text(d.JourneyId, 32) || !Text(d.PlanetId, 48) || !Text(d.Seed, 80) || !Text(d.MapId, 32) ||
                !Text(d.Error, 512) || d.ContentFingerprint == null || d.ContentFingerprint.Length != 64 ||
                d.ContentFingerprint.Any(c => !Uri.IsHexDigit(c)) || double.IsNaN(d.PhaseElapsed) ||
                double.IsInfinity(d.PhaseElapsed) || d.PhaseElapsed < 0 || d.PhaseElapsed > 1000000)
                return "航程身份、阶段或计时无效";
            if (d.Planets == null || d.Planets.Count < 1 || d.Planets.Count > 32 || d.Planets.Any(p => p == null) ||
                d.Planets.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != d.Planets.Count ||
                !d.Planets.Any(p => p.Enabled)) return "星球目录缺失、重复或超限";
            if (stableOnly && d.Phase is not (JourneyPhase.Orbit or JourneyPhase.Descent or JourneyPhase.Landed))
                return "航程切换期间不能保存";
            if (d.Phase == JourneyPhase.Orbit)
                return d.JourneyId.Length == 0 && d.PlanetId.Length == 0 && d.Seed.Length == 0 &&
                    d.MapId.Length == 0 && d.PhaseElapsed == 0 ? "" : "太空待命仍携带未提交目的地";
            if (!Guid.TryParseExact(d.JourneyId, "N", out var id) || id == Guid.Empty ||
                !Guid.TryParseExact(d.MapId, "N", out var map) || map == Guid.Empty ||
                string.IsNullOrWhiteSpace(d.Seed) || d.ActivePlanet?.Enabled != true)
                return "航程目的地或地图身份无效";
            if (d.ActivePlanet.Seed.Length != 0 && d.ActivePlanet.Seed != d.Seed) return "航程固定种子不匹配";
            if (worldId != null && OnPlanet(d) && d.MapId != worldId) return "航程与当前地图身份不匹配";
            return "";
        }

        public static bool OnPlanet(JourneyViewData d) => d != null &&
            d.Phase is JourneyPhase.ArrivalSync or JourneyPhase.Descent or JourneyPhase.Landed;

        public static string Environment(JourneyViewData journey, PlayableTerrain terrain)
        {
            if (journey == null) return "";
            if (terrain == null || !terrain.Expedition) return "航程缺少远征地图";
            if (OnPlanet(journey))
                return terrain.Seed == journey.Seed && terrain.Rooms.Count != 0 ? "" : "航程实际种子或星球地图不匹配";
            if (terrain.Seed != PlanetTerrainGenerator.SpaceSeed || terrain.Rooms.Count != 0 || terrain.Deposits.Count != 0)
                return "太空航程必须使用空载体地图";
            byte[] cells = terrain.CopyMaterials(), shapes = terrain.CopyShapes();
            bool[] protection = terrain.CopyProtection(), soft = terrain.CopySoftRock();
            int bottom = (TerrainGenerationSettings.Height - 1) * TerrainGenerationSettings.Width;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] != (i < bottom ? 0 : 8) || protection[i] != (i >= bottom) || shapes[i] != 0 || soft[i])
                    return "太空载体含有非预期地形";
            return "";
        }

        public static double MaximumCrewHeight(JourneyViewData d)
        {
            var p = OnPlanet(d) ? d.ActivePlanet : null;
            return p == null ? 384 : Math.Max(384, p.DockHeight + p.MaximumLift + ShipGeometry.Roof);
        }

        public static double MaximumGroundHeight(JourneyViewData d, double original)
        {
            var p = OnPlanet(d) ? d.ActivePlanet : null;
            return p == null ? original : Math.Max(original, p.DockHeight + original);
        }

        internal static string Relationships(ExpeditionViewData d)
        {
            var journey = d.Journey;
            if (journey == null) return "";
            string error = Validate(journey);
            if (error.Length != 0) return error;
            var ship = d.Ship;
            if (ship == null) return "航程缺少飞船";
            if (journey.Phase == JourneyPhase.Landed)
                return ship.Phase == 0 && d.Phase != 0 ? "" : "着陆阶段与远征状态不一致";
            if (ship.Phase != 3 || d.Phase != 0 || d.Crew.Any(a => !a.Boarded)) return "航行期间舱门、船员或地面状态无效";
            if (journey.Phase != JourneyPhase.Descent && (ship.VelocityX != 0 || ship.VelocityY != 0))
                return "未就绪航程仍存在推力";
            if (journey.Phase == JourneyPhase.Orbit && ship.PilotId != 0 ||
                (journey.Phase is JourneyPhase.Preparing or JourneyPhase.Transit) && ship.PilotId == 0)
                return "航程与驾驶席占用不一致";
            return "";
        }
    }
}
