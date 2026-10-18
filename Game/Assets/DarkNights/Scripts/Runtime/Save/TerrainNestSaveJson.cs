using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using Newtonsoft.Json.Linq;
using static DarkNights.Runtime.Save.SaveJsonFields;

namespace DarkNights.Runtime.Save
{
    /// <summary>巢位与单虫最终状态的显式文件合同；缺字段、未知字段、越界及阶段矛盾均拒绝，不从静态巢位复活清理结果。</summary>
    internal static class TerrainNestSaveJson
    {
        internal static JArray Layout(PlayableTerrain data) => new JArray(data.Nests.Select(n => new JObject
        { ["id"] = n.Id, ["u"] = n.U, ["row"] = n.Row, ["warning"] = n.WarningSeconds, ["speed"] = n.CrawlSpeed, ["hp"] = n.HitPoints }));
        internal static JArray Progress(PlayableTerrain data) => new JArray(data.NestProgress.Select(n => new JObject
        { ["id"] = n.Id, ["phase"] = n.Phase, ["clock"] = n.Clock, ["x"] = n.X, ["height"] = n.Height, ["vertical_speed"] = n.VerticalSpeed, ["hp"] = n.Hp }));
        internal static TerrainNest[] ReadLayout(JToken token) => Array(token, item =>
        {
            var n = Object(item);
            if (n.Count != 6) throw new FormatException("巢位字段不完整。");
            return new TerrainNest(Integer(n["id"]), Integer(n["u"]), Integer(n["row"]), Number(n["warning"]), (float)Number(n["speed"]), Integer(n["hp"]));
        }, TerrainNest.MaximumCount).ToArray();
        internal static TerrainNestProgress[] ReadProgress(JToken token) => Array(token, item =>
        {
            var n = Object(item);
            if (n.Count != 7) throw new FormatException("巢穴状态字段不完整。");
            return new TerrainNestProgress(Integer(n["id"]), Integer(n["phase"]), Number(n["clock"]), (float)Number(n["x"]),
                (float)Number(n["height"]), (float)Number(n["vertical_speed"]), Integer(n["hp"]));
        }, TerrainNest.MaximumCount).ToArray();
    }
}
