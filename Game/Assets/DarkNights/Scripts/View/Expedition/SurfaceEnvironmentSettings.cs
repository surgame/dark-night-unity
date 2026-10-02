using System;
using System.Globalization;
using UnityEngine;

namespace DarkNights.View.Expedition
{
    /// <summary>星球地表的本地美术参数；由岩壁样式保存，装配时冻结，不参与地形生成、碰撞、存档或玩法 RNG。</summary>
    [Serializable]
    public sealed class SurfaceEnvironmentSettings
    {
        [InspectorName("启用地表装饰")] public bool Enabled = true;
        [InspectorName("远山剪影")] public bool Ridges = true;
        [InspectorName("稀疏云层")] public bool Clouds = true;
        [Range(0, .5f), InspectorName("远景视差")] public float Parallax = .14f;
        [Range(2, 16), InspectorName("远山高度（格）")] public float RidgeHeight = 7;
        [Range(8, 40), InspectorName("云层高度（格）")] public float CloudHeight = 18;
        // 旧诊断夹具保留序列化字段；正式地表不读取，不再向作者展示无效过渡参数。
        [HideInInspector] public float WeatheredDepth = 1.25f;
        [HideInInspector] public float EntranceDepth = 9;
        [HideInInspector] public float UndergroundAmbient = .36f;

        public SurfaceEnvironmentSettings Capture() => new SurfaceEnvironmentSettings
        {
            Enabled = Enabled, Ridges = Ridges, Clouds = Clouds,
            Parallax = Clamp(Parallax, 0, .5f, .14f), RidgeHeight = Clamp(RidgeHeight, 2, 16, 7),
            CloudHeight = Clamp(CloudHeight, 8, 40, 18), WeatheredDepth = Clamp(WeatheredDepth, 0, 3, 1.25f),
            EntranceDepth = Clamp(EntranceDepth, 2, 20, 9), UndergroundAmbient = Clamp(UndergroundAmbient, .2f, .6f, .36f)
        };
        public string Identity
        {
            get
            {
                var value = Capture();
                return string.Join(",", "surface-scenery-v3", value.Enabled, value.Ridges, value.Clouds,
                    Number(value.Parallax), Number(value.RidgeHeight), Number(value.CloudHeight));
            }
        }
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static float Clamp(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }
}
