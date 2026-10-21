using System;
using DarkNights.Core.Config.Terrain;
using UnityEngine;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>生成作者输入的规范JSON边界；规则未启用时保持原字段顺序和字节，避免旧冻结图无故失效，启用时包含全部规则输入。</summary>
    public static class TerrainAuthoringIdentity
    {
        public static string Json(TerrainGenerationSettings settings)
        {
            if (settings == null) return "<missing>";
            if (settings.Rules?.Enabled == true) return JsonUtility.ToJson(settings);
            return JsonUtility.ToJson(new LegacyInput
            {
                Seed = settings.Seed, Surface = settings.Surface, OrganicCaves = settings.OrganicCaves,
                Amplitude = settings.Amplitude, OreDensity = settings.OreDensity, ResourceProfile = settings.ResourceProfile
            });
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
    }
}
