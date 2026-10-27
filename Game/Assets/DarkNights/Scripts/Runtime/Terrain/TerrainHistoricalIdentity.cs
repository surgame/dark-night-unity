using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DarkNights.Core.Config.Terrain;
using UnityEngine;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>已有地图及航程存档的只读身份适配器；保留原字段顺序与摘要，不生成地形，不参与新地图身份。</summary>
    internal static class TerrainHistoricalIdentity
    {
        /// <summary>原v3有效输入身份只读校验；不执行旧算法，不改写旧地图来源。</summary>
        internal static string SourceFingerprintV3(Objects.ExpeditionFlowConfig config)
        {
            string value = TerrainGenerationInputIdentity.JsonV3(config.FreezeCaveMap()) +
                "|geology-point-v1|" + (config.Geology?.Identity ?? "missing");
            using var hash = SHA256.Create();
            return "terrain-source-v3:" + BitConverter.ToString(hash.ComputeHash(
                Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>读取已保存的v2来源摘要；泊位塑形参数仅读取校验，不重新执行旧生成。</summary>
        internal static string SourceFingerprintV2(Objects.ExpeditionFlowConfig config, string planetId)
        {
            var planets = config.FreezePlanets();
            var planet = string.IsNullOrEmpty(planetId) ? planets.FirstOrDefault(p => p.Enabled) :
                planets.FirstOrDefault(p => p.Id == planetId);
            if (planet == null) throw new InvalidOperationException("地图来源缺少所选星球泊位。");
            string value = GenerationJsonV2(config.FreezeCaveMap()) + "|dock|" +
                planet.DockColumn + "|" + planet.DockRow + "|" + planet.LandingWidth +
                "|geology-point-v1|" + (config.Geology?.Identity ?? "missing");
            using var hash = SHA256.Create();
            return "terrain-source-v2:" + BitConverter.ToString(hash.ComputeHash(
                Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        internal static string FlowIdentity(Objects.ExpeditionFlowConfig config)
        {
            var text = new StringBuilder("flow-v1|");
            text.Append(config.Enabled ? '1' : '0').Append('|')
                .Append(config.PreparationTimeoutSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(config.ArrivalTimeoutSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(AuthoringJson(config.CaveMap)).Append('|').Append(config.Planets?.Count ?? -1);
            if (config.Planets != null)
                foreach (var planet in config.Planets)
                    text.Append('|').Append(planet == null ? "<missing>" : JsonUtility.ToJson(planet));
            if (config.ReviewedMap != null) text.Append("|reviewed-mine-v1|").Append(config.ReviewedMap.Fingerprint());
            text.Append("|geology-point-v1|").Append(config.Geology?.Identity ?? "missing");
            text.Append("|navigation-v1|").Append(config.Navigation == null ? "missing" : JsonUtility.ToJson(config.Navigation));
            text.Append("|terrain-modifiers-v").Append(config.ModifierSchemaVersion);
            var entries = config.CopyModifiers();
            text.Append('|').Append(entries.Count);
            foreach (var entry in entries)
                text.Append('|').Append(entry == null ? "<missing>" :
                    entry.GetType().Assembly.GetName().Name + ":" + entry.GetType().FullName)
                    .Append(':').Append(entry?.CanonicalSettings ?? "<missing>");
            return text.ToString();
        }

        internal static string SourceFingerprint(Objects.ExpeditionFlowConfig config)
        {
            config.FreezeCaveMap();
            string value = AuthoringJson(config.CaveMap) + "||" +
                string.Join(";", config.Planets.Select(p => JsonUtility.ToJson(p))) + "|" +
                string.Join(";", config.CopyModifiers().Select(m => m.GetType().FullName + ":" + m.Enabled + ":" + m.CanonicalSettings)) +
                "|geology-point-v1|" + (config.Geology?.Identity ?? "missing");
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        internal static string AuthoringJson(TerrainGenerationSettings settings)
        {
            if (settings == null) return "<missing>";
            string identity = string.IsNullOrEmpty(settings.Layout) ? Json(settings) :
                "terrain-recipe-v1|" + JsonUtility.ToJson(new RecipeInput
                {
                    Seed = settings.Seed, Surface = settings.Surface, OrganicCaves = settings.OrganicCaves,
                    Amplitude = settings.Amplitude, OreDensity = settings.OreDensity, ResourceProfile = settings.ResourceProfile,
                    Layout = settings.Layout, Structure = settings.Structure, Minerals = settings.Minerals,
                    Relief = settings.Relief, RegionOverrides = settings.RegionOverrides
                });
            var selection = (settings.SeedSelection ?? new TerrainSeedSettings()).CopyValidated();
            return selection.Mode == TerrainSeedSettings.Fixed && selection.Seeds.Length == 0 ? identity :
                identity + "|seed-selection-v1|" + JsonUtility.ToJson(selection);
        }

        public static string Json(TerrainGenerationSettings settings)
        {
            if (settings == null) return "<missing>";
            var legacy = new LegacyInput
            {
                Seed = settings.Seed, Surface = settings.Surface, OrganicCaves = settings.OrganicCaves,
                Amplitude = settings.Amplitude, OreDensity = settings.OreDensity, ResourceProfile = settings.ResourceProfile
            };
            string json = settings.Rules?.Enabled == true ? JsonUtility.ToJson(new RuleInput
            {
                Seed = settings.Seed, Surface = settings.Surface, OrganicCaves = settings.OrganicCaves,
                Amplitude = settings.Amplitude, OreDensity = settings.OreDensity, ResourceProfile = settings.ResourceProfile,
                Rules = settings.Rules
            }) : JsonUtility.ToJson(legacy);
            if (settings.Relief?.Enabled == true) json += "|surface-relief-v" + SurfaceReliefSettings.Version +
                "|" + JsonUtility.ToJson(settings.Relief.CopyValidated());
            foreach (var region in settings.RegionOverrides ?? Array.Empty<CaveRegionOverride>())
                json += "|region-content-v1|" + JsonUtility.ToJson(region.CopyValidated());
            return json;
        }

        /// <summary>旧摘要专用的序列化投影；严格保留原六字段及顺序，无运行实例或第二份作者配置。</summary>
        [Serializable]
        private sealed class LegacyInput
        {
            public string Seed;
            public string Surface;
            public bool OrganicCaves;
            public double Amplitude;
            public double OreDensity;
            public string ResourceProfile;
        }

        /// <summary>旧规则摘要字段顺序的显式投影；新增可关闭制作输入不使旧预制地图无故失效。</summary>
        [Serializable]
        private sealed class RuleInput
        {
            public string Seed;
            public string Surface;
            public bool OrganicCaves;
            public double Amplitude;
            public double OreDensity;
            public string ResourceProfile;
            public CaveRuleSettings Rules;
        }

        /// <summary>原统一配方的摘要字段投影；新增算法字段不会改变已有完整地图的旧摘要。</summary>
        [Serializable]
        private sealed class RecipeInput
        {
            public string Seed, Surface;
            public bool OrganicCaves;
            public double Amplitude, OreDensity;
            public string ResourceProfile, Layout;
            public CaveLayoutSettings Structure;
            public TerrainMineralSettings Minerals;
            public SurfaceReliefSettings Relief;
            public CaveRegionOverride[] RegionOverrides;
        }
        /// <summary>已保存v2来源的只读有效输入快照；保留旧字段，不参与当前生成或候选缓存。</summary>
        internal static string GenerationJsonV2(TerrainGenerationSettings input)
        {
            if (input == null) return "<missing>";
            // 草稿允许暂时不合法；执行入口负责校验，身份计算本身不能阻止继续编辑或Undo。
            var s = input;
            var content = s.Minerals ?? new TerrainMineralSettings();
            var relief = s.Relief ?? new SurfaceReliefSettings();
            var minerals = content.Algorithm == TerrainMineralSettings.BaseDeposits ?
                new TerrainMineralSettings() : content.Algorithm == TerrainMineralSettings.WallVeins ?
                new TerrainMineralSettings { Algorithm = content.Algorithm, VeinCount = content.VeinCount,
                    Nests = content.Nests } : content;
            return "terrain-generation-v2|" + JsonUtility.ToJson(new GenerationInputV2
            {
                Seed = s.Seed, Layout = s.Layout, SurfaceAlgorithm = string.IsNullOrEmpty(s.SurfaceAlgorithm) ?
                    (s.UsesLandform ? TerrainGenerationSettings.LandformSurface : TerrainGenerationSettings.NoiseSurface) : s.SurfaceAlgorithm,
                Surface = s.UsesLandform ? s.Surface : "", Amplitude = s.Amplitude,
                Landform = s.UsesLandform ? new LandformInputV2
                {
                    BaseHeight = relief.BaseHeightCells, Height = relief.HeightCells,
                    Wavelength = relief.WavelengthCells, DockTransition = relief.DockTransitionCells
                } : null,
                Structure = s.Layout == TerrainGenerationSettings.ConnectedRooms ? s.Structure : null,
                Minerals = minerals, Regions = s.RegionOverrides
            });
        }

        /// <summary>有效生成字段的固定序列化顺序；不承担可编辑配置或运行状态。</summary>
        [Serializable]
        private sealed class GenerationInputV2
        {
            public string Seed, Layout, SurfaceAlgorithm, Surface;
            public double Amplitude;
            public LandformInputV2 Landform;
            public CaveLayoutSettings Structure;
            public TerrainMineralSettings Minerals;
            public CaveRegionOverride[] Regions;
        }

        /// <summary>地貌算法实际消费的高度参数；排除历史启用字段。</summary>
        [Serializable]
        private sealed class LandformInputV2
        {
            public double BaseHeight, Height, Wavelength;
            public int DockTransition;
        }
    }
}
