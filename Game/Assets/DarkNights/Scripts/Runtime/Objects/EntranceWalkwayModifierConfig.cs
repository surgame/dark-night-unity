using System;
using System.Globalization;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>入口下洞步道的作者配置；关闭后仅保留天然洞穴与泊位，候选数限制连通安全搜索的开销。</summary>
    [Serializable]
    public sealed class EntranceWalkwayModifierConfig : ITerrainGenerationModifierConfig
    {
        public bool Enabled = true;
        public int Headroom = 5;
        public int MaxCandidates = 64;
        bool ITerrainGenerationModifierConfig.Enabled => Enabled;
        public string CanonicalSettings => (Enabled ? "1" : "0") + "," +
            Headroom.ToString(CultureInfo.InvariantCulture) + "," + MaxCandidates.ToString(CultureInfo.InvariantCulture);

        public ITerrainGenerationModifierConfig Copy() => new EntranceWalkwayModifierConfig
        { Enabled = Enabled, Headroom = Headroom, MaxCandidates = MaxCandidates };

        public ITerrainGenerationModifier Freeze() => new EntranceWalkwayModifier(Headroom, MaxCandidates);
    }
}
