using System;
using System.Linq;
using DarkNights.Core.Physics;
using Newtonsoft.Json.Linq;
using static DarkNights.Runtime.Save.SaveJsonFields;

namespace DarkNights.Runtime.Save
{
    /// <summary>
    /// v30物理闭包的严格JSON边界；保存连续运动与绳索，不保存输入确认、连接身份或原生缓存。
    /// 所有数组、字段、类型和有限范围在构造冻结候选前检查，解析不影响活动世界。
    /// </summary>
    internal static class PhysicsSaveJson
    {
        internal static JToken Write(PhysicsWorldSnapshot s) => s == null ? JValue.CreateNull() : new JObject
        {
            ["definition"] = s.DefinitionHash, ["tick"] = s.Tick, ["world_id"] = s.WorldId,
            ["map_epoch"] = s.MapEpoch, ["terrain_revision"] = s.TerrainRevision,
            ["bodies"] = new JArray(s.Bodies.Select(Body)),
            ["ropes"] = new JArray(s.Ropes.Select(r => new JObject
                { ["owner"] = r.OwnerId, ["target"] = r.TargetId, ["length"] = r.Length,
                    ["hanging_rope"] = r.HangingRopeId, ["position"] = r.Position }))
        };

        private static JObject Body(PhysicsBodySnapshot b) => new JObject
        {
            ["id"] = b.Id, ["x"] = b.X, ["height"] = b.Height, ["vx"] = b.VelocityX, ["vy"] = b.VelocityY,
            ["angle"] = b.Angle, ["angular_velocity"] = b.AngularVelocity, ["sleeping"] = b.Sleeping,
            ["grounded"] = b.Grounded, ["normal_x"] = b.GroundNormalX, ["normal_y"] = b.GroundNormalY,
            ["takeoff_speed"] = b.TakeoffSpeed, ["jump_buffer"] = b.JumpBuffer, ["ascending"] = b.JumpAscending
        };

        internal static PhysicsWorldSnapshot Read(JToken token)
        {
            if (token == null) throw new FormatException("缺少物理保存字段。");
            if (token.Type == JTokenType.Null) return null;
            JObject value = Object(token);
            if (value.Count != 7) throw new FormatException("物理保存字段不匹配。");
            return new PhysicsWorldSnapshot(Text(value["definition"]), (long)Unsigned(value["tick"], long.MaxValue),
                Text(value["world_id"]), Unsigned(value["map_epoch"]), Unsigned(value["terrain_revision"]),
                Array(value["bodies"], ReadBody, PhysicsNumbers.MaximumBodies),
                Array(value["ropes"], r =>
                {
                    JObject item = Object(r);
                    if (item.Count != 5) throw new FormatException("物理约束字段不匹配。");
                    return new PhysicsRopeSnapshot(Integer(item["owner"]), Integer(item["target"]), (float)Number(item["length"]),
                        Integer(item["hanging_rope"]), (float)Number(item["position"]));
                }, PhysicsNumbers.MaximumRopes));
        }

        private static PhysicsBodySnapshot ReadBody(JToken token)
        {
            JObject b = Object(token);
            if (b.Count != 14) throw new FormatException("物理体字段不匹配。");
            return new PhysicsBodySnapshot(Integer(b["id"]), (float)Number(b["x"]), (float)Number(b["height"]),
                (float)Number(b["vx"]), (float)Number(b["vy"]), (float)Number(b["angle"]),
                (float)Number(b["angular_velocity"]), Boolean(b["sleeping"]), Boolean(b["grounded"]),
                (float)Number(b["normal_x"]), (float)Number(b["normal_y"]), (float)Number(b["takeoff_speed"]),
                (float)Number(b["jump_buffer"]), Boolean(b["ascending"]));
        }

        private static ulong Unsigned(JToken token, ulong maximum = ulong.MaxValue)
        {
            if (token?.Type != JTokenType.Integer || !ulong.TryParse(token.ToString(), out ulong value) || value > maximum)
                throw new FormatException("物理版本或时间字段必须是范围内整数。");
            return value;
        }
    }
}
