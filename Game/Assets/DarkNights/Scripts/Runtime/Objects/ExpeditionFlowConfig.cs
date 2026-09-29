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
        public string Name => "太空与星球航程";
        public bool Enabled = true;
        public float PreparationTimeoutSeconds = 30;
        public float ArrivalTimeoutSeconds = 30;
        public TerrainGenerationSettings CaveMap = new TerrainGenerationSettings
        { Seed = "STRATA-0922", ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile };
        public List<PlanetPreset> Planets = new List<PlanetPreset> { new PlanetPreset() };
        public int ModifierSchemaVersion = 1;
        [SerializeReference] public List<ITerrainGenerationModifierConfig> Modifiers =
            new List<ITerrainGenerationModifierConfig> { new EntranceWalkwayModifierConfig() };

        /// <summary>冻结启用步骤和顺序；旧配置未含字段时沿用原入口步道，显式空列表则关闭所有附加步骤。</summary>
        public TerrainGenerationPipeline FreezeModifiers()
        {
            var entries = EffectiveModifiers();
            if (entries.Count > 32) throw new InvalidOperationException("地形步骤最多 32 项。");
            var steps = new List<ITerrainGenerationModifier>();
            foreach (var entry in entries)
            {
                if (entry == null) throw new InvalidOperationException("地形步骤不得为空；请删除失去类型的配置行。");
                var frozen = entry.Freeze();
                if (frozen == null) throw new InvalidOperationException("地形步骤冻结结果为空。");
                if (entry.Enabled) steps.Add(frozen);
            }
            return new TerrainGenerationPipeline(steps);
        }

        /// <summary>用于草稿保存的完整接口项复制；显式迁移旧资产后，列表为空才代表关闭所有步骤。</summary>
        public List<ITerrainGenerationModifierConfig> CopyModifiers()
        {
            return EffectiveModifiers().Select(entry => entry?.Copy()).ToList();
        }

        private IReadOnlyList<ITerrainGenerationModifierConfig> EffectiveModifiers()
        {
            if (ModifierSchemaVersion == 0 || ModifierSchemaVersion == 1 && Modifiers == null)
                return new ITerrainGenerationModifierConfig[] { new EntranceWalkwayModifierConfig() };
            if (ModifierSchemaVersion != 1)
                throw new InvalidOperationException("地形步骤配置版本或列表无效。");
            return Modifiers;
        }

        /// <summary>草稿冲突检查与内容身份使用的规范文本，明确包含接口具体类型、顺序、启用状态及参数。</summary>
        public string CanonicalIdentity()
        {
            var builder = new StringBuilder("flow-v1|");
            builder.Append(Enabled ? '1' : '0').Append('|')
                .Append(PreparationTimeoutSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(ArrivalTimeoutSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(CaveMap == null ? "<missing>" : JsonUtility.ToJson(CaveMap)).Append('|')
                .Append(Planets?.Count ?? -1);
            if (Planets != null)
                foreach (var planet in Planets)
                    builder.Append('|').Append(planet == null ? "<missing>" : JsonUtility.ToJson(planet));
            builder.Append("|terrain-modifiers-v").Append(ModifierSchemaVersion);
            var entries = EffectiveModifiers();
            builder.Append('|').Append(entries.Count);
            foreach (var entry in entries)
                builder.Append('|').Append(entry == null ? "<missing>" :
                    entry.GetType().Assembly.GetName().Name + ":" + entry.GetType().FullName)
                    .Append(':').Append(entry?.CanonicalSettings ?? "<missing>");
            return builder.ToString();
        }

        /// <summary>冻结共用洞穴输入；工作台保留预览种子，正式会话在生成时覆盖为航程种子。</summary>
        public TerrainGenerationSettings FreezeCaveMap()
        {
            if (CaveMap == null) throw new InvalidOperationException("航程缺少洞穴地图装配输入。");
            var settings = CaveMap.CopyValidated();
            if (settings.ResourceProfile != TerrainGenerationSettings.CaveExplorationProfile)
                throw new InvalidOperationException("航程地图必须使用洞穴资源方案。");
            return settings;
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

        public void Validate() { FreezePlanets(); FreezeCaveMap(); FreezeModifiers(); }

        public string Fingerprint()
        {
            Validate();
            using var hash = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes("dn-space-planet-v" + PlanetTerrainGenerator.Version + "|" + CanonicalIdentity());
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static void Timeout(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 5 || value > 180)
                throw new InvalidOperationException("航程超时须为 5–180 秒的有限数值。");
        }
    }
}
