using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkNights.Runtime.Config
{
    /// <summary>
    /// 配置解析的有界 JSON 读取规则，不做类型反射或多态反序列化。
    /// 拒绝重复键、非有限数、数值字符串和超大输入；异常保留字段路径便于定位内容错误。
    /// </summary>
    internal static class ConfigJson
    {
        public static JObject Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 1024 * 1024)
                throw new FormatException("Configuration must contain 1 to 1048576 characters.");
            using (var input = new StringReader(text))
            using (var reader = new JsonTextReader(input))
            {
                reader.MaxDepth = 32;
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;
                var value = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (reader.Read()) throw new FormatException("Trailing JSON content.");
                return value;
            }
        }

        public static JObject Object(JToken token, string key) =>
            token[key] as JObject ?? throw Error(token, key, "object required");

        public static JArray Array(JToken token, string key, bool optional = false)
        {
            if (token[key] == null && optional) return new JArray();
            return token[key] as JArray ?? throw Error(token, key, "array required");
        }

        public static string Text(JToken token, string key, bool optional = false)
        {
            JToken value = token[key];
            if (value == null && optional) return "";
            if (value == null || value.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)value))
                throw Error(token, key, "nonempty string required");
            return (string)value;
        }

        public static double Number(JToken token, string key, bool optional = false, double minimum = 0)
        {
            JToken value = token[key];
            if (value == null && optional) return 0;
            if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float))
                throw Error(token, key, "number required");
            double result = (double)value;
            if (double.IsNaN(result) || double.IsInfinity(result) || result < minimum)
                throw Error(token, key, "finite number outside allowed range");
            return result;
        }

        public static double Positive(JToken token, string key)
        {
            double result = Number(token, key);
            if (result <= 0) throw Error(token, key, "positive number required");
            return result;
        }

        public static float Width(JToken token)
        {
            double value = Positive(token, "width");
            if (value > float.MaxValue) throw Error(token, "width", "float overflow");
            return (float)value;
        }

        public static int Integer(JToken token, string key, bool optional = false, int minimum = 0)
        {
            if (token[key] == null && optional) return 0;
            if (token[key]?.Type != JTokenType.Integer) throw Error(token, key, "integer required");
            double value = Number(token, key, false, minimum);
            if (value > int.MaxValue) throw Error(token, key, "integer overflow");
            return checked((int)value);
        }

        public static ulong Seed(JToken token)
        {
            JToken value = token["seed"];
            if (value?.Type != JTokenType.Integer || !ulong.TryParse(value.ToString(), out ulong result))
                throw Error(token, "seed", "unsigned 64-bit integer required");
            return result;
        }

        public static FormatException Error(JToken token, string key, string reason) =>
            new FormatException(token.Path + "." + key + ": " + reason);
    }
}
