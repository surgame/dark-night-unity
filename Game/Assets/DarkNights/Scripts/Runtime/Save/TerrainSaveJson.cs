using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using Newtonsoft.Json.Linq;
using static DarkNights.Runtime.Save.SaveJsonFields;

namespace DarkNights.Runtime.Save
{
    /// <summary>随机地图的显式保存合同；保存最终格子和保护位，严格限制长度及材料，不依赖重新执行生成算法。</summary>
    internal static class TerrainSaveJson
    {
        internal static JToken Write(PlayableTerrain data) => data == null ? JValue.CreateNull() : new JObject
        {
            ["world_id"] = data.WorldId, ["seed"] = data.Seed,
            ["materials"] = Convert.ToBase64String(data.CopyMaterials()),
            ["protection"] = Convert.ToBase64String(data.CopyProtection().Select(v => v ? (byte)1 : (byte)0).ToArray())
        };
        internal static PlayableTerrain Read(JToken token)
        {
            if (token?.Type == JTokenType.Null) return null;
            JObject value = Object(token);
            if (value.Count != 4) throw new FormatException("地图字段不完整。");
            byte[] cells = Convert.FromBase64String(Text(value["materials"]));
            byte[] flags = Convert.FromBase64String(Text(value["protection"]));
            if (flags.Any(v => v > 1)) throw new FormatException("地图保护位无效。");
            try { return new PlayableTerrain(Text(value["world_id"]), Text(value["seed"]), cells, flags.Select(v => v == 1).ToArray()); }
            catch (ArgumentException error) { throw new FormatException("随机地图数据无效。", error); }
        }
    }
}
