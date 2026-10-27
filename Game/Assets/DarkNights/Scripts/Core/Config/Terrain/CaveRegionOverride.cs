using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>单个自然区域的制作覆盖；区域编号绑定空间身份，随机修订和可选预算仅影响该区域，不保存运行矿量。</summary>
    [Serializable]
    public sealed class CaveRegionOverride
    {
        public int Region;
        public int Revision = 1;
        public string SpaceIdentity = "";
        public string Seed = "ORE-LOCAL";
        public int VeinCount = -1;
        public int Capacity = -1;
        public double ExposedShare = -1;

        public CaveRegionOverride CopyValidated()
        {
            if (Region < 0 || Region > 21 || Revision < 1 || string.IsNullOrWhiteSpace(SpaceIdentity) ||
                string.IsNullOrWhiteSpace(Seed) || Seed.Length > 80 || VeinCount < -1 || VeinCount > 16 ||
                Capacity != -1 && (Capacity < 96 || Capacity > 2000) ||
                ExposedShare != -1 && (double.IsNaN(ExposedShare) || ExposedShare < .1 || ExposedShare > .8))
                throw new ArgumentException("区域矿物覆盖的身份、种子或预算无效。");
            return (CaveRegionOverride)MemberwiseClone();
        }
    }
}
