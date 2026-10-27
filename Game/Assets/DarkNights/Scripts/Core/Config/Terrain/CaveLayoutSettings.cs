using System;
using System.Globalization;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>连通洞室算法的空间参数；只描述入口、道路和洞室，不含版本模式开关或矿物配置。</summary>
    [Serializable]
    public sealed class CaveLayoutSettings
    {
        public const int CurrentVersion = 1;
        public const string TemplateVersion = "cave-templates-v1";
        public int Version = CurrentVersion;
        public string Templates = TemplateVersion;
        public string Entrance = "valley";
        public string Road = "erosion";
        public string RoomMix = "mixed";
        public int SpaceRevision;
        public int RoomCount = 16;
        public double RoomScale = 1;
        public int RoadRadius = 3;
        public double BranchShare = .45;
        public int BranchDepth = 3;
        public double RockGap = .12;
        public static readonly string[] Entrances = { "valley", "collapse", "hillside", "bridge", "riverbed", "boulder", "fault-step", "roots", "shaft" };
        public static readonly string[] Roads = { "erosion", "switchback", "cluster", "rift", "roots" };
        public static readonly string[] Mixes = { "mixed", "fissures", "halls", "pockets" };
        public static readonly string[] Shapes = { "low-seam", "high-arch", "fault-shelf", "slant-rift", "twin-pocket", "collapse-bowl", "layered-cavity" };

        public CaveLayoutSettings CopyValidated()
        {
            if (Version != CurrentVersion || Templates != TemplateVersion || Array.IndexOf(Entrances, Entrance) < 0 ||
                Array.IndexOf(Roads, Road) < 0 || Array.IndexOf(Mixes, RoomMix) < 0 || SpaceRevision < 0 ||
                RoomCount < 9 || RoomCount > 22 || RoadRadius < 2 || RoadRadius > 5 || BranchDepth < 1 || BranchDepth > 4 ||
                !Range(RoomScale, .65, 1.25) || !Range(BranchShare, .2, .65) || !Range(RockGap, 0, .4))
                throw new ArgumentException("洞穴结构的算法、目录或参数越界。");
            return (CaveLayoutSettings)MemberwiseClone();
        }

        public string SpaceKey(string seed) => seed + "|cave-plan-v2|" + TemplateVersion + "|" + Version + "|" + SpaceRevision + "|" +
            Road + "|" + RoomMix + "|" + RoomCount + "|" + RoomScale.ToString("R", CultureInfo.InvariantCulture) +
            "|" + BranchShare.ToString("R", CultureInfo.InvariantCulture) + "|" + BranchDepth +
            "|" + RockGap.ToString("R", CultureInfo.InvariantCulture);

        private static bool Range(double value, double min, double max) => !double.IsNaN(value) && value >= min && value <= max;
    }
}
