using System;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.ViewData;
using Newtonsoft.Json.Linq;
using static DarkNights.Runtime.Save.SaveJsonFields;

namespace DarkNights.Runtime.Save
{
    /// <summary>导航与结算摘要的严格文件边界；与世界同批捕获，连接、候选任务和本地浏览不入档。</summary>
    internal static class NavigationSaveJson
    {
        internal static JToken Write(JourneyNavigationData n)
        {
            if (n == null) throw new FormatException("保存航程缺少导航资料。");
            return new JObject
            {
                ["speed"] = n.Definition.Speed, ["descent_seconds"] = n.Definition.DescentSeconds,
                ["x"] = n.X, ["y"] = n.Y, ["run_id"] = n.RunId,
                ["settled_run_id"] = n.SettledRunId, ["last_income"] = n.LastIncome,
                ["nodes"] = new JArray(n.Definition.Nodes.Select(v => new JObject
                {
                    ["id"] = v.Id, ["name"] = v.Name, ["x"] = v.X, ["y"] = v.Y,
                    ["danger"] = v.Danger, ["risk"] = v.Risk, ["preview_only"] = v.PreviewOnly
                }))
            };
        }

        internal static JourneyNavigationData Read(JToken token)
        {
            var d = Object(token);
            if (d.Count != 8) throw new FormatException("导航文件字段数量无效。");
            try
            {
                return new JourneyNavigationData(new NavigationDefinition((float)Number(d["speed"]),
                    (float)Number(d["descent_seconds"]), Array(d["nodes"], ReadNode, 32)),
                    (float)Number(d["x"]), (float)Number(d["y"]), Text(d["run_id"]), Text(d["settled_run_id"]), Integer(d["last_income"]));
            }
            catch (ArgumentException e) { throw new FormatException("导航文件字段无效。", e); }
        }

        private static NavigationNodeDefinition ReadNode(JToken token)
        {
            var n = Object(token);
            if (n.Count != 7) throw new FormatException("导航节点字段数量无效。");
            return new NavigationNodeDefinition(Text(n["id"]), Text(n["name"]), (float)Number(n["x"]),
                (float)Number(n["y"]), Integer(n["danger"]), Text(n["risk"]), Boolean(n["preview_only"]));
        }
    }
}
