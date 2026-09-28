using System;
using System.Collections.Generic;
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

        /// <summary>冻结共用洞穴输入；工作台保留预览种子，正式会话在生成时覆盖为航程种子。</summary>
        public TerrainGenerationSettings FreezeCaveMap()
        {
            if (CaveMap == null) throw new InvalidOperationException("航程缺少洞穴地图装配输入。");
            var settings = CaveMap.CopyValidated();
            if (settings.ResourceProfile != TerrainGenerationSettings.CaveExplorationProfile)
                throw new InvalidOperationException("航程地图必须使用洞穴资源方案。");
            return settings;
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

        public void Validate() { FreezePlanets(); FreezeCaveMap(); }

        public string Fingerprint()
        {
            Validate();
            using var hash = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes("dn-space-planet-v1|" + JsonUtility.ToJson(this));
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static void Timeout(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 5 || value > 180)
                throw new InvalidOperationException("航程超时须为 5–180 秒的有限数值。");
        }
    }
}
