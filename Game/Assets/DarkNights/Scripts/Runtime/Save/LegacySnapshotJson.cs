using System;
using System.IO;
using System.Text;
using DarkNights.Core.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkNights.Runtime.Save
{
    /// <summary>
    /// 旧 schema v1 的独立 JSON 导入导出边界，保留有符号字符串表示的 64 位 RNG 位状态。
    /// 限制输入大小和深度并拒绝重复键，成功解析后仍必须经过 Core 完整字段与关系校验才能创建临时世界。
    /// </summary>
    public static class LegacySnapshotJson
    {
        public const int MaximumBytes = 4000000;

        public static SessionSnapshot Deserialize(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > MaximumBytes || Encoding.UTF8.GetByteCount(text) > MaximumBytes)
                throw new FormatException("Snapshot is empty or too large.");
            using (var input = new StringReader(text))
            using (var reader = new JsonTextReader(input))
            {
                reader.MaxDepth = 32;
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;
                var root = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (reader.Read()) throw new FormatException("Trailing JSON content.");
                return SnapshotDocumentJson.SessionSnapshot(root);
            }
        }

        public static string Serialize(SessionSnapshot saved) =>
            SnapshotDocumentJson.Write(saved ?? throw new ArgumentNullException(nameof(saved))).ToString(Formatting.None);
    }
}
