using System;
using System.Globalization;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>开发端规则洞穴的作者输入；仅启用时替代旧随机布局，与.minemap互斥。空间和内容修订独立冻结，不拥有本局状态。</summary>
    [Serializable]
    public sealed class CaveRuleSettings
    {
        public const int CurrentVersion = 1;
        public const string TemplateVersion = "cave-templates-v1";
        public bool Enabled;
        public int Version = CurrentVersion;
        public string Templates = TemplateVersion;
        public string Entrance = "valley";
        public string Road = "erosion";
        public string RoomMix = "mixed";
        public string ContentSeed = "ORE-1";
        public int SpaceRevision;
        public int ContentRevision;
        public int RoomCount = 16;
        public double RoomScale = 1;
        public int RoadRadius = 3;
        public double BranchShare = .45;
        public int BranchDepth = 3;
        public double RockGap = .12;
        public int VeinCount = 8;
        public int Capacity = 180;
        public double EmptyShare = .18;
        public double ExposedShare = .35;
        public double RichShare = .35;
        public bool Nests = true;
        public static readonly string[] Entrances = { "valley", "collapse", "hillside", "bridge", "riverbed", "boulder", "fault-step", "roots", "shaft" };
        public static readonly string[] Roads = { "erosion", "switchback", "cluster", "rift", "roots" };
        public static readonly string[] Mixes = { "mixed", "fissures", "halls", "pockets" };
        public static readonly string[] Shapes = { "low-seam", "high-arch", "fault-shelf", "slant-rift", "twin-pocket", "collapse-bowl", "layered-cavity" };

        public CaveRuleSettings CopyValidated()
        {
            if (Version != CurrentVersion || Templates != TemplateVersion || Array.IndexOf(Entrances, Entrance) < 0 || Array.IndexOf(Roads, Road) < 0 ||
                Array.IndexOf(Mixes, RoomMix) < 0 || string.IsNullOrWhiteSpace(ContentSeed) || ContentSeed.Length > 80 ||
                SpaceRevision < 0 || ContentRevision < 0 || RoomCount < 9 || RoomCount > 22 ||
                RoadRadius < 2 || RoadRadius > 5 || BranchDepth < 1 || BranchDepth > 4 || VeinCount < 1 || VeinCount > 16 ||
                Capacity < 96 || Capacity > 2000 || !Range(RoomScale, .65, 1.25) || !Range(BranchShare, .2, .65) ||
                !Range(RockGap, 0, .4) || !Range(EmptyShare, 0, .6) || !Range(ExposedShare, .1, .8) || !Range(RichShare, 0, .8))
                throw new ArgumentException("洞穴规则版本、目录、种子或制作参数越界。");
            return (CaveRuleSettings)MemberwiseClone();
        }

        public CaveRuleSettings Copy() => (CaveRuleSettings)MemberwiseClone();

        public string SpaceKey(string seed) => seed + "|" + TemplateVersion + "|" + Version + "|" + SpaceRevision + "|" +
            Entrance + "|" + Road + "|" + RoomMix + "|" + RoomCount + "|" + RoomScale.ToString("R", CultureInfo.InvariantCulture) +
            "|" + RoadRadius + "|" + BranchShare.ToString("R", CultureInfo.InvariantCulture) + "|" + BranchDepth +
            "|" + RockGap.ToString("R", CultureInfo.InvariantCulture);

        public string ContentKey => ContentSeed + "|content-v1|" + ContentRevision;
        private static bool Range(double value, double min, double max) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= min && value <= max;
    }
}
