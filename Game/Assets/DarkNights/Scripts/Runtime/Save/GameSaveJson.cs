using System;
using System.IO;
using System.Linq;
using System.Text;
using DarkNights.Core.Config;
using DarkNights.Core.Logic;
using DarkNights.Core.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkNights.Runtime.Save
{
    /// <summary>
    /// Unity 世界存档 v1 的显式编解码边界，绑定实际目录及布局并检查格式、随机算法与兼容摘要。
    /// 只保存冻结世界；相机、选择、权限和网络会话身份不入档。恢复只返回经完整校验的新世界，由会话层决定替换时机。
    /// </summary>
    public sealed class GameSaveJson
    {
        public const int MaximumBytes = 4000000;
        public const string Format = "dark-nights.world";
        public const int FormatVersion = 1;
        public const string RandomAlgorithm = SimulationRandom.Algorithm;
        private readonly GameCatalog catalog;
        private readonly LevelLayout layout;
        private readonly SaveContentFingerprint fingerprint;
        private static readonly string[] WorldFields =
        {
            "level_id", "economy", "wave", "elapsed", "speed", "paused", "next_entity_id",
            "rng_seed", "rng_state", "actors", "buildings", "worksites", "projectiles", "stats"
        };

        public GameSaveJson(GameCatalog catalog, LevelLayout layout)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            fingerprint = new SaveContentFingerprint(catalog, layout);
        }

        public string Serialize(SessionSnapshot snapshot)
        {
            string error = SnapshotValidator.Validate(snapshot, catalog, layout);
            if (error.Length != 0) throw new ArgumentException(error, nameof(snapshot));
            JObject world = SnapshotDocumentJson.Write(snapshot);
            world.Remove("schema_version");
            world.Remove("camera_x");
            world.Remove("camera_zoom");
            world.Remove("selected_ids");
            string text = new JObject
            {
                ["format"] = Format,
                ["format_version"] = FormatVersion,
                ["random_algorithm"] = RandomAlgorithm,
                ["rules_sha256"] = fingerprint.RulesSha256,
                ["layout_sha256"] = fingerprint.LayoutSha256,
                ["world"] = world
            }.ToString(Formatting.None);
            CheckSize(text);
            return text;
        }

        public GameSession Restore(string text)
        {
            JObject root = Parse(text);
            if (root.Count != 6 || SaveJsonFields.Text(root["format"]) != Format ||
                SaveJsonFields.Integer(root["format_version"]) != FormatVersion)
                throw new FormatException("Unsupported Dark Nights save format.");
            if (SaveJsonFields.Text(root["random_algorithm"]) != RandomAlgorithm)
                throw new FormatException("Incompatible random algorithm.");
            if (SaveJsonFields.Text(root["rules_sha256"]) != fingerprint.RulesSha256 ||
                SaveJsonFields.Text(root["layout_sha256"]) != fingerprint.LayoutSha256)
                throw new FormatException("Save rules or layout do not match the active content.");
            JObject world = SaveJsonFields.Object(root["world"]);
            if (world.Count != WorldFields.Length || world.Properties().Any(property => !WorldFields.Contains(property.Name)))
                throw new FormatException("Invalid world fields.");
            // 仅适配已验证的 Core 快照合同；旧本地显示默认值不进入世界也不恢复到客户端。
            world["schema_version"] = 1;
            world["camera_x"] = layout.CameraX;
            world["camera_zoom"] = 2.8f;
            world["selected_ids"] = new JArray();
            return SnapshotMapper.Restore(SnapshotDocumentJson.SessionSnapshot(world), catalog, layout);
        }

        public GameSession ImportLegacy(string text) =>
            SnapshotMapper.Restore(LegacySnapshotJson.Deserialize(text), catalog, layout);

        private static JObject Parse(string text)
        {
            CheckSize(text);
            using (var input = new StringReader(text))
            using (var reader = new JsonTextReader(input))
            {
                reader.MaxDepth = 32;
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;
                JObject root = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (reader.Read()) throw new FormatException("Trailing JSON content.");
                return root;
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
