using System;

namespace DarkNights.View.Lighting
{
    /// <summary>当前客户端的照明表现调参；不写权威状态或存档，硬柔阴影与墙内羽化各自独立。</summary>
    [Serializable]
    public sealed class ExplorationLightSettings
    {
        public bool SoftShadows = true;
        public float Ambient = .24f;
        public float Softness = .22f;
        public float ConeFeather = .045f;
        public float WallDepth = .375f;
        public float WallStrength = .65f;
        public float NearStrength = 1;
        public float Bounce = .12f;
        public float Relief = .22f;
        public float RangeScale = 1;
        public float ConeOffset;
        public float IntensityScale = 1;
    }
}
