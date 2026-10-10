using System;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>场景光照配置内的渲染参数；单灯数值由各预设和 Definition 覆盖提供，不含业务状态或玩家存档。</summary>
    [Serializable]
    public sealed class ExplorationLightSettings
    {
        public LightingBackendKind Backend = LightingBackendKind.PrivateField;
        [Range(0, 1)] public float Ambient = .24f;
        [Range(0, 1)] public float WallDepth = .375f;
        [Range(0, 1)] public float WallStrength = .65f;
        [Range(0, .4f)] public float Bounce = .12f;
        [Range(0, 1)] public float Relief = .22f;

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(LightingBackendKind), Backend) || !Valid(Ambient, 0, 1) ||
                !Valid(WallDepth, 0, 1) || !Valid(WallStrength, 0, 1) || !Valid(Bounce, 0, .4f) || !Valid(Relief, 0, 1))
                throw new ArgumentException("场景光照参数无效。");
        }

        private static bool Valid(float value, float low, float high) => float.IsFinite(value) && value >= low && value <= high;
    }
}
