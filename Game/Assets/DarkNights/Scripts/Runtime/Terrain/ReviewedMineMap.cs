using System;
using System.Linq;
using System.Security.Cryptography;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>开发端冻结的联合矿巢地图资源；运行只加载最终布局，不重新抽矿或巢位。内容摘要参与会话身份，已有存档独立保存最终状态。</summary>
    public sealed class ReviewedMineMap : ScriptableObject
    {
        public const int FormatVersion = 2;
        public TextAsset Content;
        public string Recipe;
        public string ContentSha256;
        public string SourceIdentity;
        [NonSerialized] private TextAsset verifiedContent;
        [NonSerialized] private string verifiedExpected;
        [NonSerialized] private Hash128 verifiedImport;
        public static string SourceFingerprint(Objects.ExpeditionFlowConfig config)
        {
            string value = JsonUtility.ToJson(config.FreezeCaveMap()) + "|" + (config.MineMap == null ? "" : JsonUtility.ToJson(config.MineMap.Freeze())) + "|" +
                string.Join(";", config.Planets.Select(p => JsonUtility.ToJson(p))) + "|" +
                string.Join(";", config.CopyModifiers().Select(m => m.GetType().FullName + ":" + m.Enabled + ":" + m.CanonicalSettings)) +
                "|geology-point-v1|" + (config.Geology?.Identity ?? "missing");
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
        public void VerifySource(Objects.ExpeditionFlowConfig config)
        {
            if (SourceIdentity != SourceFingerprint(config))
                throw new InvalidOperationException("基础地形或泊位参数已变化，请在矿脉与巢穴页生成新的联合地图后再开局。");
        }
        public string Fingerprint()
        {
            if (Content == null) throw new InvalidOperationException("联合地图内容缺失。");
            Hash128 imported = default;
#if UNITY_EDITOR
            imported = UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(Content));
#endif
            if (verifiedContent == Content && verifiedExpected == ContentSha256 && verifiedImport == imported) return ContentSha256;
            byte[] bytes = Content.bytes;
            if (bytes.Length > 2000000) throw new InvalidOperationException("联合地图内容超限。");
            using var hash = SHA256.Create();
            var actual = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            if (actual != ContentSha256) throw new InvalidOperationException("联合地图内容摘要不一致，请重新生成新版本。");
            verifiedContent = Content; verifiedExpected = ContentSha256; verifiedImport = imported;
            return actual;
        }
        public PlayableTerrain Freeze(string worldId)
        {
            Fingerprint();
            var root = JObject.Parse(Content.text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (root.Count != 3 || (int?)root["version"] != FormatVersion || (string)root["recipe"] != Recipe)
                throw new FormatException("联合地图版本或配方不匹配。");
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
                throw new FormatException("联合地图必须是未运行的星球静态布局。");
            foreach (var site in map.GeologySites)
            {
                string reason = GeologySiteValidation.Check(map, site);
                if (reason.Length != 0) throw new FormatException(reason);
            }
            var background = map.Background == null ? null : new BackgroundBakeDescriptor(worldId, map.Seed,
                map.Background.CopyMaterials(), map.Background.CopyShapes());
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
