using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Save;
using Newtonsoft.Json.Linq;
using static DarkNights.Runtime.Save.SaveJsonFields;
using static DarkNights.Runtime.Save.SnapshotDocumentJson;

namespace DarkNights.Runtime.Save
{
    /// <summary>
    /// 显式映射实体及队列字段，不依赖反射或类型名；构造不可变快照并保留全部旧档必需字段，供 IL2CPP 使用。
    /// </summary>
    internal static class SnapshotEntityJson
    {
        public static ActorSnapshot ActorSnapshot(JToken value)
        {
            JObject v = Object(value);
            return new ActorSnapshot(
                Integer(v["id"]),
                Text(v["kind"]),
                Boolean(v["enemy"]),
                Text(v["name"]),
                Number(v["x"]),
                Number(v["hp"]),
                Activity(v["state"]),
                Integer(v["target_id"]),
                Number(v["move_x"]),
                Number(v["rally_x"]),
                Number(v["face"]),
                Number(v["action_time"]),
                Number(v["attack_clock"]),
                Number(v["windup"]),
                Boolean(v["hit_pending"]),
                Boolean(v["forced_attack"]),
                Number(v["ai_clock"]),
                (float)Number(v["height"]),
                (float)Number(v["vertical_speed"]),
                Integer(v["support_platform"]),
                Integer(v["ignored_platform"]),
                Number(v["drop_remaining"]),
                Boolean(v["manual_control"]),
                Integer(v["selected_item"]),
                Integer(v["selection_revision"]),
                Boolean(v["jetpack_equipped"]),
                Number(v["jetpack_fuel"]), (float)Number(v["aim_angle"]), Number(v["equipment_cooldown"]), Number(v["equipment_action"]), Number(v["equipment_action_duration"]));
        }

        public static JObject Write(ActorSnapshot v) => new JObject
        {
            ["id"] = v.Id,
            ["kind"] = v.Kind,
            ["enemy"] = v.Enemy,
            ["name"] = v.Name,
            ["x"] = v.X,
            ["hp"] = v.Hp,
            ["state"] = EnumText(v.State.ToString()),
            ["target_id"] = v.TargetId,
            ["move_x"] = v.MoveX,
            ["rally_x"] = v.RallyX,
            ["face"] = v.Face,
            ["action_time"] = v.ActionTime,
            ["attack_clock"] = v.AttackClock,
            ["windup"] = v.Windup,
            ["hit_pending"] = v.HitPending,
            ["forced_attack"] = v.ForcedAttack,
            ["ai_clock"] = v.AiClock,
            ["height"] = v.Height,
            ["vertical_speed"] = v.VerticalSpeed,
            ["support_platform"] = v.SupportPlatform,
            ["ignored_platform"] = v.IgnoredPlatform,
            ["drop_remaining"] = v.DropRemaining,
            ["manual_control"] = v.ManualControl,
            ["selected_item"] = v.SelectedItem,
            ["selection_revision"] = v.SelectionRevision,
            ["jetpack_equipped"] = v.JetpackEquipped,
            ["jetpack_fuel"] = v.JetpackFuel,
            ["aim_angle"] = v.AimAngle,
            ["equipment_cooldown"] = v.EquipmentCooldown,
            ["equipment_action"] = v.EquipmentAction,
            ["equipment_action_duration"] = v.EquipmentActionDuration
        };

        public static BuildingSnapshot BuildingSnapshot(JToken value)
        {
            JObject v = Object(value);
            return new BuildingSnapshot(
                Integer(v["id"]),
                Text(v["kind"]),
                Number(v["x"]),
                Number(v["hp"]),
                Number(v["progress"]),
                Integer(v["worker_id"]),
                Number(v["attack_clock"]),
                Array(v["training_queue"], TrainingSnapshot, 256));
        }

        public static JObject Write(BuildingSnapshot v) => new JObject
        {
            ["id"] = v.Id,
            ["kind"] = v.Kind,
            ["x"] = v.X,
            ["hp"] = v.Hp,
            ["progress"] = v.Progress,
            ["worker_id"] = v.WorkerId,
            ["attack_clock"] = v.AttackClock,
            ["training_queue"] = new JArray(v.TrainingQueue.Select(Write))
        };

        public static WorksiteSnapshot WorksiteSnapshot(JToken value)
        {
            JObject v = Object(value);
            return new WorksiteSnapshot(
                Integer(v["id"]),
                Text(v["kind"]),
                Number(v["x"]),
                Integer(v["worker_id"]),
                Integer(v["amount"]),
                Number(v["progress"]),
                Integer(v["variant"]),
                Integer(v["farm_id"]));
        }

        public static JObject Write(WorksiteSnapshot v) => new JObject
        {
            ["id"] = v.Id,
            ["kind"] = v.Kind,
            ["x"] = v.X,
            ["worker_id"] = v.WorkerId,
            ["amount"] = v.Amount,
            ["progress"] = v.Progress,
            ["variant"] = v.Variant,
            ["farm_id"] = v.FarmId
        };

        public static ProjectileSnapshot ProjectileSnapshot(JToken value)
        {
            JObject v = Object(value);
            return new ProjectileSnapshot(
                Array(v["from"], Number, 2),
                Array(v["to"], Number, 2),
                Integer(v["target_id"]),
                Integer(v["damage"]),
                Number(v["age"]),
                Number(v["duration"]), Integer(v["kind"]), (float)Number(v["velocity_x"]), (float)Number(v["velocity_y"]), (float)Number(v["gravity"]), (float)Number(v["radius"]), (float)Number(v["blast_radius"]), Boolean(v["stuck"]));
        }

        public static JObject Write(ProjectileSnapshot v) => new JObject
        {
            ["from"] = new JArray(v.From),
            ["to"] = new JArray(v.To),
            ["target_id"] = v.TargetId,
            ["damage"] = v.Damage,
            ["age"] = v.Age,
            ["duration"] = v.Duration,
            ["kind"] = v.Kind,
            ["velocity_x"] = v.VelocityX,
            ["velocity_y"] = v.VelocityY,
            ["gravity"] = v.Gravity,
            ["radius"] = v.Radius,
            ["blast_radius"] = v.BlastRadius,
            ["stuck"] = v.Stuck
        };

        public static TrainingSnapshot TrainingSnapshot(JToken value)
        {
            JObject v = Object(value);
            return new TrainingSnapshot(
                Integer(v["actor_id"]),
                Text(v["kind"]),
                Number(v["remaining"]));
        }

        public static JObject Write(TrainingSnapshot v) => new JObject
        {
            ["actor_id"] = v.ActorId,
            ["kind"] = v.Kind,
            ["remaining"] = v.Remaining
        };

    }
}
