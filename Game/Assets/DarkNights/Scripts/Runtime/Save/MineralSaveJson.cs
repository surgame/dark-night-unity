using System;
using System.Linq;
using DarkNights.Core.Save;
using DarkNights.Core.ViewData;
using Newtonsoft.Json.Linq;
using static DarkNights.Runtime.Save.SaveJsonFields;

namespace DarkNights.Runtime.Save
{
    /// <summary>专用矿床的显式 v18 JSON 映射；格列表有界，拒绝未知字段与非法内容版本，不读取旧工位矿床格式。</summary>
    internal static class MineralSaveJson
    {
        internal static JObject Write(MineralDepositSnapshot value) => new JObject
        {
            ["id"] = value.Id, ["x"] = value.X, ["y"] = value.Y, ["room_kind"] = value.RoomKind, ["rarity"] = value.Rarity,
            ["cells"] = new JArray(value.Cells.Select(cell => new JObject
            { ["u"] = cell.U, ["v"] = cell.V, ["capacity"] = cell.Capacity, ["remaining"] = cell.Remaining,
                ["durability"] = cell.Durability, ["content_version"] = (long)cell.ContentVersion }))
        };

        internal static MineralDepositSnapshot Read(JToken input)
        {
            var value = Object(input);
            if (value.Count != 6) throw new FormatException("矿床保存字段不完整。");
            return new MineralDepositSnapshot(Integer(value["id"]), Number(value["x"]), Integer(value["y"]),
                Text(value["room_kind"]), Text(value["rarity"]), Array(value["cells"], entry =>
                {
                    var cell = Object(entry);
                    if (cell.Count != 6) throw new FormatException("矿格保存字段不完整。");
                    int version = Integer(cell["content_version"]);
                    if (version < 1 || version > 2) throw new FormatException("矿格内容版本无效。");
                    return new MineralCellViewData(Integer(cell["u"]), Integer(cell["v"]), Integer(cell["capacity"]),
                        Integer(cell["remaining"]), Integer(cell["durability"]), (ulong)version);
                }, 64).ToArray());
        }
    }
}
