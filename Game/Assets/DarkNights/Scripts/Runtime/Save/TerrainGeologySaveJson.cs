using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using Newtonsoft.Json.Linq;
using static DarkNights.Runtime.Save.SaveJsonFields;

namespace DarkNights.Runtime.Save
{
    /// <summary>地图塌方站点与事件进度的显式文件合同；首片最多一点，静态清单与本局阶段分开冻结，缺字段或未知字段均拒绝。</summary>
    internal static class TerrainGeologySaveJson
    {
        internal static JObject Site(TerrainGeologySite value) => new JObject
        {
            ["site_id"] = value.SiteId, ["definition"] = value.DefinitionGuid, ["definition_hash"] = value.DefinitionHash,
            ["configuration"] = value.Configuration, ["association"] = value.Association,
            ["allowed_u"] = value.AllowedU, ["allowed_v"] = value.AllowedV, ["allowed_width"] = value.AllowedWidth, ["allowed_height"] = value.AllowedHeight,
            ["retreat_u"] = value.RetreatU, ["retreat_v"] = value.RetreatV, ["retreat_width"] = value.RetreatWidth, ["retreat_height"] = value.RetreatHeight
        };
        internal static TerrainGeologySite ReadSite(JToken token)
        {
            var value = Object(token);
            if (value.Count != 13) throw new FormatException("塌方站点字段不完整。");
            var site = new TerrainGeologySite(Text(value["site_id"]), Text(value["definition"]), Text(value["definition_hash"]),
                Text(value["configuration"]), Text(value["association"]), Integer(value["allowed_u"]), Integer(value["allowed_v"]),
                Integer(value["allowed_width"]), Integer(value["allowed_height"]), Integer(value["retreat_u"]), Integer(value["retreat_v"]),
                Integer(value["retreat_width"]), Integer(value["retreat_height"]));
            Objects.GeologySiteConfiguration.Resolve(site);
            return site;
        }
        internal static JArray Sites(PlayableTerrain map) => new JArray(map.GeologySites.Select(Site));
        internal static TerrainGeologySite[] ReadSites(JToken token) => Array(token, ReadSite, 1).ToArray();
        internal static JArray Progress(PlayableTerrain map) => new JArray(map.GeologyProgress.Select(value => new JObject
        { ["site_id"] = value.SiteId, ["phase"] = value.Phase, ["clock"] = value.Clock, ["reason"] = value.Reason }));
        internal static TerrainGeologyProgress[] ReadProgress(JToken token) => Array(token, item =>
        {
            var value = Object(item);
            if (value.Count != 4) throw new FormatException("塌方进度字段不完整。");
            return new TerrainGeologyProgress(Text(value["site_id"]), Integer(value["phase"]), Number(value["clock"]), Text(value["reason"]));
        }, 1).ToArray();
    }
}
