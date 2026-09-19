using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DarkNights.Core.Config;
using DarkNights.Core.Logic;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Save;
using DarkNights.Core.ViewData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static DarkNights.Runtime.Save.SaveJsonFields;

namespace DarkNights.Runtime.Save
{
    /// <summary>
    /// 统一 YYGC 世界的 v6 冻结存档边界；只解析 DTO，不创建对象或修改当前会话。
    /// 拒绝旧版本、未知字段、内容摘要和身份关系不符；保存不包含权限、相机或连接身份。
    /// </summary>
    public sealed class ObjectWorldSaveJson
    {
        public const int FormatVersion = 6;
        public const int MaximumBytes = 4000000;
        public const string Format = "dark-nights.world";
        private readonly GameCatalog catalog;
        private readonly LevelLayout layout;
        private readonly SaveContentFingerprint fingerprint;
        private readonly Dictionary<string, string> definitions;
        private readonly Dictionary<string, string> placements;
        public string IdentitySha256 { get; }

        public ObjectWorldSaveJson(GameCatalog catalog, LevelLayout layout,
            IReadOnlyDictionary<string, string> definitions, IReadOnlyDictionary<string, string> placements)
        {
            this.catalog = catalog;
            this.layout = layout;
            this.definitions = definitions.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            this.placements = placements.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            fingerprint = new SaveContentFingerprint(catalog, layout);
            var identity = new JObject
            {
                ["definitions"] = new JObject(this.definitions.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => new JProperty(p.Key, p.Value))),
                ["placements"] = new JObject(this.placements.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => new JProperty(p.Key, p.Value)))
            };
            using var hash = SHA256.Create();
            IdentitySha256 = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity.ToString(Formatting.None))))
                .Replace("-", "").ToLowerInvariant();
        }

        public string Serialize(SessionSnapshot snapshot)
        {
            Validate(snapshot);
            string text = new JObject
            {
                ["format"] = Format, ["format_version"] = FormatVersion,
                ["random_algorithm"] = SimulationRandom.Algorithm,
                ["rules_sha256"] = fingerprint.RulesSha256, ["layout_sha256"] = fingerprint.LayoutSha256,
                ["identity_sha256"] = IdentitySha256, ["world"] = World(snapshot)
            }.ToString(Formatting.None);
            CheckSize(text);
            return text;
        }

        public SessionSnapshot Parse(string text)
        {
            CheckSize(text);
            JObject root;
            using (var input = new StringReader(text))
            using (var reader = new JsonTextReader(input) { MaxDepth = 32, DateParseHandling = DateParseHandling.None })
            {
                root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new FormatException("Trailing save content.");
            }
            if (root["format"]?.Type != JTokenType.String || (string)root["format"] != Format ||
                root["format_version"]?.Type != JTokenType.Integer || (long)root["format_version"] != FormatVersion)
                throw new FormatException("不支持的存档版本。");
            if (root.Count != 7 || Text(root["random_algorithm"]) != SimulationRandom.Algorithm ||
                Text(root["rules_sha256"]) != fingerprint.RulesSha256 || Text(root["layout_sha256"]) != fingerprint.LayoutSha256 ||
                Text(root["identity_sha256"]) != IdentitySha256)
                throw new FormatException("Save content does not match this game.");
            JObject world = Object(root["world"]);
            if (!Enum.TryParse(Text(world["mode"]), out SessionMode mode) || !Enum.IsDefined(typeof(SessionMode), mode))
                throw new FormatException("Invalid session mode.");
            var identities = Array(world["identities"], item =>
            {
                JObject entry = Object(item);
                if (entry.Count != 3) throw new FormatException("Invalid identity fields.");
                return new EntityIdentityData(Integer(entry["id"]), Text(entry["definition_guid"]), Text(entry["placement_key"]));
            }, 256);
            var snapshot = new SessionSnapshot(FormatVersion, Text(world["level_id"]),
                SnapshotDocumentJson.EconomySnapshot(world["economy"]), SnapshotDocumentJson.WaveSnapshot(world["wave"]),
                Number(world["elapsed"]), Number(world["speed"]), Boolean(world["paused"]), Integer(world["next_entity_id"]),
                Text(world["rng_seed"]), Text(world["rng_state"]),
                Array(world["actors"], SnapshotEntityJson.ActorSnapshot, 256),
                Array(world["buildings"], SnapshotEntityJson.BuildingSnapshot, 256),
                Array(world["worksites"], SnapshotEntityJson.WorksiteSnapshot, 256),
                Array(world["projectiles"], SnapshotEntityJson.ProjectileSnapshot, 1024),
                SnapshotDocumentJson.StatisticsSnapshot(world["stats"]), mode, identities, TerrainSaveJson.Read(world["terrain"]));
            Validate(snapshot);
            RequireFields(world, World(snapshot));
            return snapshot;
        }

        private void Validate(SessionSnapshot snapshot)
        {
            if (snapshot?.SchemaVersion != FormatVersion) throw new FormatException("不支持的存档版本。");
            string error = SnapshotValidator.Validate(snapshot, catalog, layout);
            if (error.Length != 0) throw new FormatException(error);
            var kinds = snapshot.Actors.Select(a => (a.Id, a.Kind)).Concat(snapshot.Buildings.Select(b => (b.Id, b.Kind)))
                .Concat(snapshot.Worksites.Select(w => (w.Id, w.Kind))).ToDictionary(p => p.Id, p => p.Kind);
            foreach (EntityIdentityData identity in snapshot.Identities)
            {
                string kind = kinds[identity.Id];
                if (!definitions.TryGetValue(kind, out string guid) || guid != identity.DefinitionGuid)
                    throw new FormatException("Entity definition does not match its RuleKey.");
                if (identity.PlacementKey.Length == 0) continue;
                if (identity.PlacementKey.StartsWith("terrain.deposit.", StringComparison.Ordinal))
                {
                    if (kind != "mineral-deposit") throw new FormatException("Terrain deposit identity has the wrong RuleKey.");
                    continue;
                }
                if (!placements.TryGetValue(identity.PlacementKey, out string original) ||
                    (original != kind && !(catalog.Balance.Units.ContainsKey(original) && catalog.Balance.Units.ContainsKey(kind))))
                    throw new FormatException("Unknown or incompatible scene placement identity.");
            }
        }

        private static JObject World(SessionSnapshot snapshot)
        {
            JObject world = SnapshotDocumentJson.Write(snapshot);
            world["mode"] = snapshot.Mode.ToString();
            world["identities"] = new JArray(snapshot.Identities.Select(i => new JObject
            {
                ["id"] = i.Id, ["definition_guid"] = i.DefinitionGuid, ["placement_key"] = i.PlacementKey
            }));
            return world;
        }

        private static void RequireFields(JToken actual, JToken expected)
        {
            if (actual is JObject obj && expected is JObject model)
            {
                if (obj.Count != model.Count || obj.Properties().Any(p => model.Property(p.Name) == null))
                    throw new FormatException("Unknown or missing world fields.");
                foreach (JProperty property in obj.Properties()) RequireFields(property.Value, model[property.Name]);
            }
            else if (actual is JArray array && expected is JArray models)
            {
                if (array.Count != models.Count) throw new FormatException("Invalid world array.");
                for (int index = 0; index < array.Count; index++) RequireFields(array[index], models[index]);
            }
        }

        private static void CheckSize(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > MaximumBytes ||
                Encoding.UTF8.GetByteCount(text) > MaximumBytes)
                throw new FormatException("Save is empty or too large.");
        }
    }
}
