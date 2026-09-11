using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Save;
using Newtonsoft.Json.Linq;
using static DarkNights.Runtime.Save.SaveJsonFields;
using static DarkNights.Runtime.Save.SnapshotEntityJson;

namespace DarkNights.Runtime.Save
{
    /// <summary>
    /// 显式映射存档根与经济波次字段，不依赖反射或类型名；构造不可变快照并保留全部旧档必需字段，供 IL2CPP 使用。
    /// </summary>
    internal static class SnapshotDocumentJson
    {
        public static SessionSnapshot SessionSnapshot(JToken value)
        {
            JObject v = Object(value);
            return new SessionSnapshot(
                Integer(v["schema_version"]),
                Text(v["level_id"]),
                EconomySnapshot(v["economy"]),
                WaveSnapshot(v["wave"]),
                Number(v["elapsed"]),
                Number(v["speed"]),
                Boolean(v["paused"]),
                Integer(v["next_entity_id"]),
                Text(v["rng_seed"]),
                Text(v["rng_state"]),
                Array(v["actors"], ActorSnapshot, 256),
                Array(v["buildings"], BuildingSnapshot, 256),
                Array(v["worksites"], WorksiteSnapshot, 256),
                Array(v["projectiles"], ProjectileSnapshot, 1024),
                StatisticsSnapshot(v["stats"]),
                Number(v["camera_x"]),
                Number(v["camera_zoom"]),
                Array(v["selected_ids"], Integer, 256));
        }

        public static JObject Write(SessionSnapshot v) => new JObject
        {
            ["schema_version"] = v.SchemaVersion,
            ["level_id"] = v.LevelId,
            ["economy"] = Write(v.Economy),
            ["wave"] = Write(v.Wave),
            ["elapsed"] = v.Elapsed,
            ["speed"] = v.Speed,
            ["paused"] = v.Paused,
            ["next_entity_id"] = v.NextEntityId,
            ["rng_seed"] = v.RngSeed,
            ["rng_state"] = v.RngState,
            ["actors"] = new JArray(v.Actors.Select(SnapshotEntityJson.Write)),
            ["buildings"] = new JArray(v.Buildings.Select(SnapshotEntityJson.Write)),
            ["worksites"] = new JArray(v.Worksites.Select(SnapshotEntityJson.Write)),
            ["projectiles"] = new JArray(v.Projectiles.Select(SnapshotEntityJson.Write)),
            ["stats"] = Write(v.Stats),
            ["camera_x"] = v.CameraX,
            ["camera_zoom"] = v.CameraZoom,
            ["selected_ids"] = new JArray(v.SelectedIds)
        };

        public static EconomySnapshot EconomySnapshot(JToken value)
        {
            JObject v = Object(value);
            return new EconomySnapshot(
                ResourceAmounts(v["resources"]),
                Number(v["upkeep_elapsed"]),
                Number(v["starvation_elapsed"]),
                Number(v["recruit_cooldown"]));
        }

        public static JObject Write(EconomySnapshot v) => new JObject
        {
            ["resources"] = Write(v.Resources),
            ["upkeep_elapsed"] = v.UpkeepElapsed,
            ["starvation_elapsed"] = v.StarvationElapsed,
            ["recruit_cooldown"] = v.RecruitCooldown
        };

        public static WaveSnapshot WaveSnapshot(JToken value)
        {
            JObject v = Object(value);
            return new WaveSnapshot(
                Integer(v["index"]),
                Phase(v["phase"]),
                Number(v["day_remaining"]),
                Number(v["spawn_elapsed"]),
                Integer(v["next_spawn"]));
        }

        public static JObject Write(WaveSnapshot v) => new JObject
        {
            ["index"] = v.Index,
            ["phase"] = EnumText(v.Phase.ToString()),
            ["day_remaining"] = v.DayRemaining,
            ["spawn_elapsed"] = v.SpawnElapsed,
            ["next_spawn"] = v.NextSpawn
        };

        public static StatisticsSnapshot StatisticsSnapshot(JToken value)
        {
            JObject v = Object(value);
            return new StatisticsSnapshot(
                Integer(v["kills"]),
                Integer(v["lost"]),
                ResourceAmounts(v["gathered"]));
        }

        public static JObject Write(StatisticsSnapshot v) => new JObject
        {
            ["kills"] = v.Kills,
            ["lost"] = v.Lost,
            ["gathered"] = Write(v.Gathered)
        };

        public static ResourceAmounts ResourceAmounts(JToken value)
        {
            JObject v = Object(value);
            if (v.Count != 5) throw new FormatException("Invalid resource fields.");
            return new ResourceAmounts(
                Number(v["food"]),
                Number(v["wood"]),
                Number(v["stone"]),
                Number(v["iron"]),
                Number(v["gold"]));
        }

        public static JObject Write(ResourceAmounts v) => new JObject
        {
            ["food"] = v.Food,
            ["wood"] = v.Wood,
            ["stone"] = v.Stone,
            ["iron"] = v.Iron,
            ["gold"] = v.Gold
        };

    }
}
