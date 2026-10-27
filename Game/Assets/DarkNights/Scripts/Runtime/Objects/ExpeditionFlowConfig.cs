using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DarkNights.Core.Logic.Terrain;
using System.Security.Cryptography;
using System.Text;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using GameCore.Objects.Behaviours.Interfaces;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 太空到星球航程的 YYGC 共享配置；作者来源为正式会话 ObjectDefinition.SharedConfigs。
    /// 进入会话前校验并冻结星球目录，指纹参与内容身份；不保存航程阶段或驾驶席等实例状态。
    /// </summary>
    [Serializable]
    public sealed class ExpeditionFlowConfig : IConfigData
    {
        private const int FrozenMapFingerprintVersion = 5;
        private const int CurrentFrozenMapFingerprintVersion = 6;
        public string Name => "太空与星球航程";
        public bool Enabled = true;
        public NavigationPreset Navigation = new NavigationPreset();
        public float PreparationTimeoutSeconds = 30;
        public float ArrivalTimeoutSeconds = 30;
        public TerrainGenerationSettings CaveMap = new TerrainGenerationSettings
        { Seed = "STRATA-0922", ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile };
        public Terrain.ReviewedMineMap ReviewedMap;
        public GeologyDesignPoint Geology = new GeologyDesignPoint();
        public List<PlanetPreset> Planets = new List<PlanetPreset> { new PlanetPreset() };
        [HideInInspector] public int ModifierSchemaVersion = 1;
        [SerializeReference, HideInInspector] public List<ITerrainGenerationModifierConfig> Modifiers =
            new List<ITerrainGenerationModifierConfig>();

        /// <summary>附加地形塑形暂停；旧步骤只保留来源身份和参数，所有布局均返回空流水线。</summary>
        public TerrainGenerationPipeline FreezeModifiers()
        {
            return TerrainGenerationPipeline.Default;
        }

        /// <summary>复制完整作者记录；保留已退场步骤的参数，使旧文档来源校验及独立草稿复制不丢数据。</summary>
        public List<ITerrainGenerationModifierConfig> CopyModifiers()
        {
            return EffectiveModifiers().Select(entry => entry?.Copy()).ToList();
        }

        private IReadOnlyList<ITerrainGenerationModifierConfig> EffectiveModifiers()
        {
            // 旧来源摘要包含此隐式记录；保留身份，执行时由 FreezeModifiers 排除。
            if (ModifierSchemaVersion == 0 || ModifierSchemaVersion == 1 && Modifiers == null)
                return new ITerrainGenerationModifierConfig[] { new EntranceWalkwayModifierConfig() };
            if (ModifierSchemaVersion != 1)
                throw new InvalidOperationException("地形步骤配置版本或列表无效。");
            return Modifiers;
        }

        /// <summary>完整当前配置的规范文本；用于草稿及会话身份，包含制作策略和航程，排除退场地形步骤。</summary>
        public string CanonicalIdentity()
        {
            var builder = new StringBuilder("flow-v2|");
            builder.Append(Enabled ? '1' : '0').Append('|')
                .Append(PreparationTimeoutSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(ArrivalTimeoutSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(Terrain.TerrainAuthoringIdentity.Json(CaveMap)).Append('|')
                .Append(Planets?.Count ?? -1);
            if (Planets != null)
                foreach (var planet in Planets)
                    builder.Append('|').Append(planet == null ? "<missing>" : JsonUtility.ToJson(planet));
            if (ReviewedMap != null) builder.Append("|reviewed-mine-v1|").Append(ReviewedMap.Fingerprint());
            builder.Append("|geology-point-v1|").Append(Geology?.Identity ?? "missing");
            builder.Append("|navigation-v1|").Append(Navigation == null ? "missing" : JsonUtility.ToJson(Navigation));
            return builder.ToString();
        }

        /// <summary>冻结同一地图规则及制作种子策略；此处不抽样，正式远征只加载已保存的完整地图。</summary>
        public TerrainGenerationSettings FreezeCaveMap()
        {
            if (CaveMap == null) throw new InvalidOperationException("航程缺少洞穴地图装配输入。");
            var settings = CaveMap.CopyValidated();
            if (settings.ResourceProfile != TerrainGenerationSettings.CaveExplorationProfile)
                throw new InvalidOperationException("航程地图必须使用洞穴资源方案。");
            return settings.CopyValidated();
        }

        /// <summary>工作台选择正式星球；未指定时使用首个启用项，不构造另一份默认泊位配置。</summary>
        public PlanetDefinition PreviewPlanet(string id = null)
        {
            var planets = FreezePlanets();
            return planets.FirstOrDefault(p => p.Enabled && (string.IsNullOrEmpty(id) || p.Id == id))
                ?? throw new InvalidOperationException("没有可预览的正式星球。");
        }

        public PlanetDefinition[] FreezePlanets()
        {
            if (Planets == null || Planets.Count > 32 || Enabled && Planets.Count == 0)
                throw new InvalidOperationException("星球目录最多 32 行，启用流程时不得为空。");
            Timeout(PreparationTimeoutSeconds); Timeout(ArrivalTimeoutSeconds);
            var result = new PlanetDefinition[Planets.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            bool available = false;
            for (int i = 0; i < Planets.Count; i++)
            {
                result[i] = Planets[i]?.Freeze() ?? throw new InvalidOperationException("星球行不得为空。");
                if (!ids.Add(result[i].Id)) throw new InvalidOperationException("星球 ID 重复：" + result[i].Id);
                available |= result[i].Enabled;
            }
            if (Enabled && !available) throw new InvalidOperationException("启用流程时至少保留一个可选星球。");
            return result;
        }

        public void Validate()
        {
            var planets = FreezePlanets(); FreezeCaveMap();
            if (Navigation == null) throw new InvalidOperationException("航程缺少导航配置。");
            Navigation.Freeze(planets);
        }

        public string Fingerprint()
        {
            Validate();
            using var hash = SHA256.Create();
            // 冻结图不再运行生成器；沿用既有身份，避免仅退场步道就拒绝原地图的航程存档。
            bool historical = ReviewedMap?.HasHistoricalSource == true;
            int version = ReviewedMap == null ? PlanetTerrainGenerator.Version :
                historical ? FrozenMapFingerprintVersion : CurrentFrozenMapFingerprintVersion;
            byte[] bytes = Encoding.UTF8.GetBytes("dn-space-planet-v" + version + "|" +
                (historical ? Terrain.TerrainHistoricalIdentity.FlowIdentity(this) : CanonicalIdentity()));
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static void Timeout(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 5 || value > 180)
                throw new InvalidOperationException("航程超时须为 5–180 秒的有限数值。");
        }
    }
}
