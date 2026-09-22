using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using Newtonsoft.Json.Linq;
using static DarkNights.Runtime.Save.SaveJsonFields;

namespace DarkNights.Runtime.Save
{
    /// <summary>随机地图的显式保存合同；保存最终格子、静态玩法标记和保护位，不依赖重新执行生成算法。</summary>
    internal static class TerrainSaveJson
    {
        internal static JToken Write(PlayableTerrain data)
        {
            if (data == null) return JValue.CreateNull();
            return new JObject
            {
                ["world_id"] = data.WorldId, ["seed"] = data.Seed,
                ["background"] = data.Background == null ? JValue.CreateNull() :
                    new JValue(Convert.ToBase64String(Terrain.BackgroundReferenceCodec.Encode(data.Background))),
                ["expedition"] = data.Expedition, ["shapes"] = Convert.ToBase64String(data.CopyShapes()),
                ["materials"] = Convert.ToBase64String(data.CopyMaterials()),
                ["protection"] = Convert.ToBase64String(data.CopyProtection().Select(v => v ? (byte)1 : (byte)0).ToArray()),
                ["soft_rock"] = Convert.ToBase64String(data.CopySoftRock().Select(v => v ? (byte)1 : (byte)0).ToArray()),
                ["rooms"] = new JArray(data.Rooms.Select(room => new JObject
                {
                    ["kind"] = room.Kind, ["x"] = room.X, ["y"] = room.Y, ["width"] = room.Width,
                    ["height"] = room.Height, ["features"] = (int)room.Features,
                    ["deposit_budget"] = room.DepositBudget, ["soft_rock_radius"] = room.SoftRockRadius
                })),
                ["deposits"] = new JArray(data.Deposits.Select(deposit => new JObject
                {
                    ["id"] = deposit.Id, ["room_kind"] = deposit.RoomKind, ["x"] = deposit.X, ["y"] = deposit.Y,
                    ["rarity"] = deposit.Rarity, ["capacity"] = deposit.Capacity
                }))
            };
        }
        internal static PlayableTerrain Read(JToken token)
        {
            if (token?.Type == JTokenType.Null) return null;
            JObject value = Object(token);
            if (value.Count != 10) throw new FormatException("地图字段不完整，缺少初始背景参考合同。");
            byte[] cells = Convert.FromBase64String(Text(value["materials"]));
            byte[] flags = Convert.FromBase64String(Text(value["protection"]));
            byte[] soft = Convert.FromBase64String(Text(value["soft_rock"]));
            if (flags.Length != cells.Length || soft.Length != cells.Length || flags.Any(v => v > 1) || soft.Any(v => v > 1))
                throw new FormatException("地图保护位或软岩位无效。");
            var rooms = Array(value["rooms"], item =>
            {
                JObject room = Object(item);
                if (room.Count != 8) throw new FormatException("地图洞室字段不完整。");
                var features = (TerrainRoomFeature)Integer(room["features"]);
                if (((int)features & ~63) != 0) throw new FormatException("地图洞室标签无效。");
                return new TerrainRoom(Text(room["kind"]), Integer(room["x"]), Integer(room["y"]),
                    Integer(room["width"]), Integer(room["height"]), features,
                    Integer(room["deposit_budget"]), Integer(room["soft_rock_radius"]));
            }, 32).ToArray();
            var deposits = Array(value["deposits"], item =>
            {
                JObject deposit = Object(item);
                if (deposit.Count != 6) throw new FormatException("地图矿床字段不完整。");
                return new TerrainDepositBlueprint(Text(deposit["id"]), Text(deposit["room_kind"]),
                    Integer(deposit["x"]), Integer(deposit["y"]), Text(deposit["rarity"]), Integer(deposit["capacity"]));
            }, 128).ToArray();
            try
            {
                var background = value["background"]?.Type == JTokenType.Null ? null :
                    Terrain.BackgroundReferenceCodec.Decode(Convert.FromBase64String(Text(value["background"])));
                if (Boolean(value["expedition"]) && background == null) throw new FormatException("远征存档缺少初始背景参考。");
                return new PlayableTerrain(Text(value["world_id"]), Text(value["seed"]), cells,
                    flags.Select(v => v == 1).ToArray(), soft.Select(v => v == 1).ToArray(), rooms, deposits,
                    Convert.FromBase64String(Text(value["shapes"])), Boolean(value["expedition"]), background);
            }
            catch (ArgumentException error) { throw new FormatException("随机地图数据无效。", error); }
        }
    }
}
