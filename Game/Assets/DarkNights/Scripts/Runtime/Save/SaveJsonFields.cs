using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DarkNights.Core.Logic.State;
using Newtonsoft.Json.Linq;

namespace DarkNights.Runtime.Save
{
    /// <summary>
    /// 存档字段的严格类型边界，拒绝缺失、null、数值字符串、非法枚举和超限数组。
    /// 只产生本次解析拥有的值，不使用多态反序列化，也不访问或替换权威世界。
    /// </summary>
    internal static class SaveJsonFields
    {
        public static JObject Object(JToken token) => token as JObject ?? throw new FormatException("Object required.");

        public static double Number(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
                throw new FormatException("Number required.");
            double value = (double)token;
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new FormatException("Finite number required.");
            return value;
        }

        public static int Integer(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer)
                throw new FormatException("Integer required.");
            double value = Number(token);
            if (value < int.MinValue || value > int.MaxValue) throw new FormatException("Integer out of range.");
            return (int)token;
        }

        public static string Text(JToken token)
        {
            if (token == null || token.Type != JTokenType.String) throw new FormatException("String required.");
            return (string)token;
        }

        public static bool Boolean(JToken token)
        {
            if (token == null || token.Type != JTokenType.Boolean) throw new FormatException("Boolean required.");
            return (bool)token;
        }

        public static IReadOnlyList<T> Array<T>(JToken token, Func<JToken, T> convert, int maximum)
        {
            if (!(token is JArray array) || array.Count > maximum) throw new FormatException("Invalid array.");
            var result = new List<T>(array.Count);
            foreach (var value in array) result.Add(convert(value));
            return result;
        }

        public static ActorActivity Activity(JToken token)
        {
            string text = Text(token);
            foreach (ActorActivity value in Enum.GetValues(typeof(ActorActivity)))
                if (EnumText(value.ToString()) == text) return value;
            throw new FormatException("Invalid actor activity.");
        }

        public static WavePhase Phase(JToken token) => Text(token) switch
        {
            "day" => WavePhase.Day,
            "night" => WavePhase.Night,
            _ => throw new FormatException("Invalid wave phase.")
        };

        public static string EnumText(string value) => Regex.Replace(value, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
    }
}
