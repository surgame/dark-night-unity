using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>开发端冻结的已保存地图资源；运行只加载最终布局，不重新抽矿或巢位。内容摘要参与会话身份，已有存档独立保存最终状态。</summary>
    public sealed class ReviewedMineMap : ScriptableObject
    {
        public const int FormatVersion = 2;
        public TextAsset Content;
        public string Recipe;
        public string ContentSha256;
        public string SourceIdentity;
        public string SourcePlanetId;
        public bool HasHistoricalSource => string.IsNullOrEmpty(SourceIdentity) ||
            !SourceIdentity.StartsWith("terrain-source-v2:", StringComparison.Ordinal) &&
            !SourceIdentity.StartsWith("terrain-source-v3:", StringComparison.Ordinal) &&
            !SourceIdentity.StartsWith("terrain-source-v4:", StringComparison.Ordinal);
        [NonSerialized] private TextAsset verifiedContent;
        [NonSerialized] private string verifiedExpected;
        [NonSerialized] private Hash128 verifiedImport;
        public static string SourceFingerprint(Objects.ExpeditionFlowConfig config, string planetId = null)
        {
            var planets = config.FreezePlanets();
            var planet = string.IsNullOrEmpty(planetId) ? planets.FirstOrDefault(p => p.Enabled) :
                planets.FirstOrDefault(p => p.Id == planetId);
            if (planet == null) throw new InvalidOperationException("地图来源缺少所选星球。");
            string value = TerrainGenerationInputIdentity.Json(config.FreezeCaveMap()) +
                "|geology-point-v1|" + (config.Geology?.Identity ?? "missing");
            using var hash = SHA256.Create();
            return "terrain-source-v4:" + BitConverter.ToString(hash.ComputeHash(
                System.Text.Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
        public void VerifySource(Objects.ExpeditionFlowConfig config)
        {
            string expected = HasHistoricalSource ? TerrainHistoricalIdentity.SourceFingerprint(config) :
                SourceIdentity.StartsWith("terrain-source-v2:", StringComparison.Ordinal) ?
                TerrainHistoricalIdentity.SourceFingerprintV2(config, SourcePlanetId) :
                SourceIdentity.StartsWith("terrain-source-v3:", StringComparison.Ordinal) ?
                TerrainHistoricalIdentity.SourceFingerprintV3(config) : SourceFingerprint(config, SourcePlanetId);
            if (SourceIdentity != expected)
                throw new InvalidOperationException("地图来源输入已变化，请在地图设计工作台保存地图后再用于新局。");
        }
        public string Fingerprint()
        {
            if (Content == null) throw new InvalidOperationException("完整地图内容缺失。");
            Hash128 imported = default;
#if UNITY_EDITOR
            imported = UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(Content));
#endif
            if (verifiedContent == Content && verifiedExpected == ContentSha256 && verifiedImport == imported) return ContentSha256;
            byte[] bytes = Content.bytes;
            if (bytes.Length > 2000000) throw new InvalidOperationException("完整地图内容超限。");
            using var hash = SHA256.Create();
            var actual = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            if (actual != ContentSha256) throw new InvalidOperationException("完整地图内容摘要不一致，请重新生成新版本。");
            verifiedContent = Content; verifiedExpected = ContentSha256; verifiedImport = imported;
            return actual;
        }
        public PlayableTerrain Freeze(string worldId)
        {
            return Decode(CaptureContent(), Recipe, worldId);
        }
        /// <summary>在Unity主线程核对来源并捕获不可变文本；后台解码不得再访问TextAsset或AssetDatabase。</summary>
        public string CaptureContent()
        {
            Fingerprint();
            return Content.text;
        }
        /// <summary>仅解码已捕获的冻结文本，不接触Unity对象；同步运行加载与Editor后台读取共用相同严格校验。</summary>
        public static PlayableTerrain Decode(string content, string recipe, string worldId, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            var root = JObject.Parse(content, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (root.Count != 3 || (int?)root["version"] != FormatVersion || (string)root["recipe"] != recipe)
                throw new FormatException("完整地图版本或配方不匹配。");
            var terrain = root["terrain"] as JObject ?? throw new FormatException("静态地图字段无效。");
            if (terrain.Count != 16 || terrain["world_id"] != null) throw new FormatException("静态地图不得携带本局WorldId。");
            terrain["world_id"] = worldId;
            var staticBackground = terrain["background"] as JObject ?? throw new FormatException("静态地图缺少背景初值。");
            if (staticBackground.Count != 2) throw new FormatException("静态背景字段无效。");
            var frozenBackground = new BackgroundBakeDescriptor(worldId, (string)terrain["seed"],
                Convert.FromBase64String((string)staticBackground["materials"]), Convert.FromBase64String((string)staticBackground["shapes"]));
            terrain["background"] = Convert.ToBase64String(BackgroundReferenceCodec.Encode(frozenBackground));
            var map = TerrainSaveJson.Read(terrain);
            if (map == null || !map.Expedition || map.Minerals != null || map.Damage.Count != 0 || map.NestProgress.Count != 0 || map.GeologyProgress.Count != 0)
                throw new FormatException("完整地图必须是未运行的星球静态布局。");
            foreach (var site in map.GeologySites)
            {
                cancellation.ThrowIfCancellationRequested();
                string reason = GeologySiteValidation.Check(map, site);
                if (reason.Length != 0) throw new FormatException(reason);
            }
            var background = map.Background == null ? null : new BackgroundBakeDescriptor(worldId, map.Seed,
                map.Background.CopyMaterials(), map.Background.CopyShapes());
            cancellation.ThrowIfCancellationRequested();
            return new PlayableTerrain(worldId, map.Seed, map.CopyMaterials(), map.CopyProtection(), map.CopySoftRock(),
                map.Rooms.ToArray(), map.Deposits.ToArray(), map.CopyShapes(), true, background, nests: map.Nests.ToArray(), geologySites: map.GeologySites.ToArray());
        }
        public static string Encode(PlayableTerrain map, string recipe)
        {
            if (map.Background == null || map.Minerals != null || map.Damage.Count != 0 || map.NestProgress.Count != 0 || map.GeologyProgress.Count != 0)
                throw new ArgumentException("仅允许导出完整静态候选。");
            var terrain = (JObject)TerrainSaveJson.Write(map);
            terrain.Remove("world_id");
            terrain["background"] = new JObject { ["materials"] = Convert.ToBase64String(map.Background.CopyMaterials()),
                ["shapes"] = Convert.ToBase64String(map.Background.CopyShapes()) };
            return new JObject { ["version"] = FormatVersion, ["recipe"] = recipe, ["terrain"] = terrain }.ToString(Formatting.None);
        }
    }
}
