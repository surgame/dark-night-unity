using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DarkNights.Core.Config.Terrain;
using GameCore.Objects.Definition;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>静态站点到现有地质配置的严格适配；坐标使用现行逻辑格合同，冻结后统一读取轨迹，不复用样板中的地图位置或缓存。</summary>
    public static class GeologySiteConfiguration
    {
        private static readonly System.Collections.Generic.Dictionary<string, GeologyEventConfig> cache =
            new System.Collections.Generic.Dictionary<string, GeologyEventConfig>(StringComparer.Ordinal);
        public static string DefinitionHash(ObjectDefinition definition)
        {
            if (definition == null || definition.PrefabRef == null ||
                !definition.BehaviourTypes.Contains(typeof(GeologyEventBehaviour).FullName) ||
                definition.SharedConfigs.OfType<GeologyEventConfig>().Count() != 1)
                throw new ArgumentException("塌方Definition缺少既有地质能力、Prefab或唯一配置。");
            string identity = definition.Guid + "|" + definition.PrefabRef.AssetGUID + "|" +
                string.Join(";", definition.BehaviourTypes) + "|" + JsonUtility.ToJson(definition.SharedConfigs.OfType<GeologyEventConfig>().Single());
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").ToLowerInvariant();
        }

        public static GeologyEventConfig Resolve(TerrainGeologySite site)
        {
            lock (cache)
            {
                if (cache.TryGetValue(site.Configuration, out var known) && known.EventId == site.SiteId) return known.Freeze();
            }
            var json = JObject.Parse(site.Configuration, new Newtonsoft.Json.Linq.JsonLoadSettings
            { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            string[] fields = { "EventId", "EnterRegion", "SupportU", "SupportV", "RegionU", "RegionV", "RegionWidth", "RegionHeight",
                "SourceU", "SourceV", "LandingU", "LandingV", "Width", "SourceRows", "RubbleWidth", "Gravity", "WarningSeconds",
                "FragmentDelay", "OccupancyWaitSeconds", "Material" };
            if (json.Count != fields.Length || fields.Any(name => json[name] == null)) throw new FormatException("塌方配置字段不完整。");
            foreach (string field in fields)
            {
                var type = json[field].Type;
                if (field == "EventId" || field == "Material")
                { if (type != JTokenType.String) throw new FormatException("塌方字符串字段类型无效。"); }
                else if (field == "EnterRegion")
                { if (type != JTokenType.Boolean) throw new FormatException("塌方触发方式类型无效。"); }
                else if (field == "Gravity" || field == "WarningSeconds" || field == "FragmentDelay" || field == "OccupancyWaitSeconds")
                { if (type != JTokenType.Float && type != JTokenType.Integer) throw new FormatException("塌方时长／重力类型无效。"); }
                else if (type != JTokenType.Integer) throw new FormatException("塌方格坐标字段类型无效。");
            }
            var config = JsonUtility.FromJson<GeologyEventConfig>(site.Configuration);
            if (config.EventId != site.SiteId) throw new FormatException("塌方站点与配置身份不一致。");
            var frozen = config.Freeze();
            lock (cache)
            {
                if (cache.Count >= 8) cache.Clear();
                cache[site.Configuration] = frozen;
            }
            return frozen.Freeze();
        }
    }
}
