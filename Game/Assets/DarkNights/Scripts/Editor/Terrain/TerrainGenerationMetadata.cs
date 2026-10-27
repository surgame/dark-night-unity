using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地图文档专属的制作来源编码；保存同次空间计划与阶段摘要，不改运行地图格式或玩家存档。
    /// 旧文档缺少此信息时返回空；读取只核对保存数据，不调用现版规划器合成历史连线。
    /// </summary>
    internal static class TerrainGenerationMetadata
    {
        internal static string UpgradeBaseline(string baseline, string current)
        {
            if (string.IsNullOrEmpty(baseline)) return current;
            var before = JObject.Parse(baseline);
            var after = JObject.Parse(current);
            var document = before["MonoBehaviour"] as JObject;
            var actual = after["MonoBehaviour"] as JObject;
            if (document == null || actual == null) return baseline;
            if (document["GenerationMetadata"] == null && (string)actual["GenerationMetadata"] == "")
                document["GenerationMetadata"] = "";
            var map = document["Input"]?["CaveMap"] as JObject;
            if (map != null && map["DisableValleySculpt"] == null &&
                (bool?)actual["Input"]?["CaveMap"]?["DisableValleySculpt"] == false)
                map["DisableValleySculpt"] = false;
            return JToken.DeepEquals(before, after) ? current : baseline;
        }

        internal static string Encode(TerrainGenerationResult result, string mapHash)
        {
            if (result == null) return "";
            var plan = result.Plan;
            var rooms = new JArray();
            var edges = new JArray();
            if (plan != null)
            {
                foreach (var room in plan.Rooms)
                    rooms.Add(new JArray(room.Kind, room.X, room.Y, room.Width, room.Height));
                foreach (var edge in plan.Connections)
                    edges.Add(new JArray(edge.From, edge.To, (int)edge.Kind, edge.BendX, edge.BendY, edge.Radius, edge.CoverLength));
            }
            return new JObject
            {
                ["schema"] = 1, ["generator"] = result.Version, ["map"] = mapHash, ["seed"] = result.Terrain.Seed,
                ["space"] = result.SpaceIdentity, ["rooms"] = rooms, ["connections"] = edges,
                ["levels"] = new JArray(plan?.BranchLevels ?? Array.Empty<int>()),
                ["distances"] = new JArray(plan?.Distances ?? Array.Empty<double>()),
                ["diagnostics"] = new JArray(plan?.Diagnostics ?? Array.Empty<string>()),
                ["steps"] = new JArray(result.Steps.Select(s => new JObject
                {
                    ["name"] = s.Name, ["cells"] = s.ChangedCells, ["shapes"] = s.ChangedShapes,
                    ["ore"] = s.MineralCells, ["nests"] = s.Nests,
                    ["bounds"] = new JArray(s.Left, s.Top, s.Right, s.Bottom)
                }))
            }.ToString(Formatting.None);
        }

        internal static TerrainGenerationResult Decode(string text, PlayableTerrain map, string mapHash)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (text.Length > 100000) throw new FormatException("制作来源信息超限。");
            var data = JObject.Parse(text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if ((int?)data["schema"] != 1 || (string)data["map"] != mapHash || (string)data["seed"] != map.Seed ||
                (int?)data["generator"] < 1)
                throw new FormatException("制作来源与已保存地图不匹配。");
            var rows = (JArray)data["rooms"];
            CaveRulePlan plan = null;
            if (rows.Count > 0)
            {
                if (rows.Count != map.Rooms.Count || rows.Count > 22) throw new FormatException("制作洞室清单与地图不符。");
                var rooms = rows.Select(r => new TerrainRoom((string)r[0], (int)r[1], (int)r[2], (int)r[3], (int)r[4])).ToArray();
                for (int i = 0; i < rooms.Length; i++)
                {
                    var a = rooms[i]; var b = map.Rooms[i];
                    if (a.Kind != b.Kind || a.X != b.X || a.Y != b.Y || a.Width != b.Width || a.Height != b.Height)
                        throw new FormatException("制作洞室坐标与地图不符。");
                }
                var edges = ((JArray)data["connections"]).Select(e => new CavePassage((int)e[0], (int)e[1],
                    (CavePassageKind)(int)e[2], (int)e[3], (int)e[4], (int)e[5], (int)e[6])).ToArray();
                var levels = ((JArray)data["levels"]).Values<int>().ToArray();
                var distances = ((JArray)data["distances"]).Values<double>().ToArray();
                if (edges.Length > 64 || levels.Length != rooms.Length || distances.Length != rooms.Length ||
                    edges.Any(e => e.From < 0 || e.To < 0 || e.From >= rooms.Length || e.To >= rooms.Length) ||
                    distances.Any(x => double.IsNaN(x) || double.IsInfinity(x) || x < 0))
                    throw new FormatException("制作连接或分区数据无效。");
                plan = new CaveRulePlan(rooms, edges, levels, distances, ((JArray)data["diagnostics"]).Values<string>().ToArray());
            }
            var steps = ((JArray)data["steps"]).Select(s => new TerrainGenerationStep((string)s["name"],
                (int)s["cells"], (int)s["shapes"], (int)s["ore"], (int)s["nests"],
                (int)s["bounds"][0], (int)s["bounds"][1], (int)s["bounds"][2], (int)s["bounds"][3])).ToArray();
            if (steps.Length != 8) throw new FormatException("制作步骤清单无效。");
            return new TerrainGenerationResult(map, plan, (int)data["generator"], (string)data["space"], steps);
        }
    }
}
