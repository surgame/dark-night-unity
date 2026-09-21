using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>地图生成的可序列化输入；独立于营地规则，生成前校验并冻结，不作为运行状态。</summary>
    [Serializable]
    public sealed class TerrainGenerationSettings
    {
        public string Seed = "GREYPINE-1616";
        public string Surface = "needles";
        public bool OrganicCaves;
        public double Amplitude = 1;
        public double OreDensity = 1;
        public int CaveColumnSpacing = 43;
        public int CaveRowSpacing = 24;
        public double CaveRoomWidthScale = .78;
        public double CaveRoomHeightScale = .62;
        public int CavePassageRadius = 2;
        public string ResourceProfile = GameplayResourceProfile;
        public const int GeneratorVersion = 1;
        public const int Width = 320;
        public const int Height = 192;
        public const string CaveExplorationProfile = "cave-exploration";
        public const string GameplayResourceProfile = "dark-nights";
        public const string ReferenceResourceProfile = "reference";
        public static readonly string[] SurfaceNames = { "needles", "terraces", "karst", "basin", "rolling", "broken" };

        public TerrainGenerationSettings CopyValidated()
        {
            if (string.IsNullOrWhiteSpace(Seed) || Seed.Length > 80) throw new ArgumentException("种子长度必须为 1–80。");
            if (Array.IndexOf(SurfaceNames, Surface) < 0) throw new ArgumentException("未知地表算法。");
            if (ResourceProfile != GameplayResourceProfile && ResourceProfile != ReferenceResourceProfile && ResourceProfile != CaveExplorationProfile)
                throw new ArgumentException("未知资源分布方案。");
            if (double.IsNaN(Amplitude) || Amplitude < .3 || Amplitude > 1.6 ||
                double.IsNaN(OreDensity) || OreDensity < .2 || OreDensity > 2)
                throw new ArgumentOutOfRangeException(nameof(Amplitude), "地表起伏或矿脉密度越界。");
            if (ResourceProfile == CaveExplorationProfile &&
                (CaveColumnSpacing < 34 || CaveColumnSpacing > 60 || CaveRowSpacing < 18 || CaveRowSpacing > 36 ||
                 double.IsNaN(CaveRoomWidthScale) || CaveRoomWidthScale < .45 || CaveRoomWidthScale > 1.25 ||
                 double.IsNaN(CaveRoomHeightScale) || CaveRoomHeightScale < .45 || CaveRoomHeightScale > 1.25 ||
                 CavePassageRadius < 2 || CavePassageRadius > 4))
                throw new ArgumentOutOfRangeException(nameof(CaveColumnSpacing), "洞穴间距、洞室比例或通路宽度越界。");
            return new TerrainGenerationSettings { Seed = Seed, Surface = Surface, OrganicCaves = OrganicCaves,
                Amplitude = Amplitude, OreDensity = OreDensity, ResourceProfile = ResourceProfile,
                CaveColumnSpacing = CaveColumnSpacing, CaveRowSpacing = CaveRowSpacing,
                CaveRoomWidthScale = CaveRoomWidthScale, CaveRoomHeightScale = CaveRoomHeightScale,
                CavePassageRadius = CavePassageRadius };
        }

        /// <summary>恢复参考 HTML 尺度的紧凑洞穴参数；只改变下一次纯生成输入，不触碰运行地图。</summary>
        public void UseCompactCaveDefaults()
        {
            CaveColumnSpacing = 43; CaveRowSpacing = 24;
            CaveRoomWidthScale = .78; CaveRoomHeightScale = .62; CavePassageRadius = 2;
        }

        /// <summary>恢复 2026-09-20 天然洞穴实验的宽松参数，供工作台并排比较，不作为正式默认。</summary>
        public void UseLegacyCaveScale()
        {
            CaveColumnSpacing = 53; CaveRowSpacing = 30;
            CaveRoomWidthScale = 1; CaveRoomHeightScale = 1; CavePassageRadius = 3;
        }

        /// <summary>创建仅供历史 HTML 栅格向量使用的旧资源分布输入；不进入正式随机地图。</summary>
        public TerrainGenerationSettings AsReferenceProfile()
        {
            var copy = CopyValidated();
            copy.ResourceProfile = ReferenceResourceProfile;
            return copy;
        }
    }
}
