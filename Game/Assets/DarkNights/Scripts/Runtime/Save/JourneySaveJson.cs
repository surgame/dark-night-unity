using System;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Save;
using DarkNights.Core.ViewData;
using Newtonsoft.Json.Linq;
using static DarkNights.Runtime.Save.SaveJsonFields;

namespace DarkNights.Runtime.Save
{
    /// <summary>航程与星球目录的严格 JSON 边界；显式字段和最多 32 行目录，不使用反射、多态类型名或网络类型恢复配置。</summary>
    internal static class JourneySaveJson
    {
        internal static JToken Write(JourneyViewData d) => d == null ? JValue.CreateNull() : new JObject
        {
            ["Enabled"] = d.Enabled, ["JourneyId"] = d.JourneyId, ["Revision"] = d.Revision,
            ["Phase"] = (int)d.Phase, ["PlanetId"] = d.PlanetId, ["Seed"] = d.Seed, ["MapId"] = d.MapId,
            ["ContentFingerprint"] = d.ContentFingerprint, ["PhaseElapsed"] = d.PhaseElapsed,
            ["Error"] = d.Error, ["Planets"] = new JArray(d.Planets.Select(WritePlanet))
        };

        internal static JourneyViewData Read(JToken token)
        {
            if (token?.Type == JTokenType.Null) return null;
            var d = Object(token);
            if (d.Count != 11) throw new FormatException("航程字段数量无效。");
            var value = new JourneyViewData(Boolean(d["Enabled"]), Text(d["JourneyId"]), Integer(d["Revision"]),
                (JourneyPhase)Integer(d["Phase"]), Text(d["PlanetId"]), Text(d["Seed"]), Text(d["MapId"]),
                Text(d["ContentFingerprint"]), Number(d["PhaseElapsed"]), Text(d["Error"]), Array(d["Planets"], ReadPlanet, 32));
            string error = JourneyValidator.Validate(value, stableOnly: true);
            if (error.Length != 0) throw new FormatException(error);
            return value;
        }

        private static JToken WritePlanet(PlanetDefinition p) => new JObject
        {
            ["Id"] = p.Id, ["DisplayName"] = p.DisplayName, ["Description"] = p.Description, ["Enabled"] = p.Enabled,
            ["Seed"] = p.Seed, ["DockColumn"] = p.DockColumn, ["DockRow"] = p.DockRow, ["LandingWidth"] = p.LandingWidth,
            ["ArrivalHeight"] = p.ArrivalHeight, ["HorizontalRange"] = p.HorizontalRange, ["MaximumLift"] = p.MaximumLift,
            ["TransitSeconds"] = p.TransitSeconds, ["StarCount"] = p.StarCount, ["StarSpeed"] = p.StarSpeed,
            ["TransitionKind"] = p.TransitionKind, ["SpaceColorHex"] = p.SpaceColorHex, ["SkyColorHex"] = p.SkyColorHex
        };

        private static PlanetDefinition ReadPlanet(JToken token)
        {
            var p = Object(token);
            if (p.Count != 17) throw new FormatException("星球字段数量无效。");
            try
            {
                return new PlanetDefinition(Text(p["Id"]), Text(p["DisplayName"]), Text(p["Description"]),
                    Boolean(p["Enabled"]), Text(p["Seed"]), Integer(p["DockColumn"]), Integer(p["DockRow"]),
                    Integer(p["LandingWidth"]), (float)Number(p["ArrivalHeight"]), (float)Number(p["HorizontalRange"]),
                    (float)Number(p["MaximumLift"]), (float)Number(p["TransitSeconds"]), Integer(p["StarCount"]),
                    (float)Number(p["StarSpeed"]), Text(p["TransitionKind"]), Text(p["SpaceColorHex"]), Text(p["SkyColorHex"]));
            }
            catch (ArgumentException e) { throw new FormatException("存档星球配置无效。", e); }
        }
    }
}
