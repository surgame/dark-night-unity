using System;
using System.Globalization;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>已退场入口步道的序列化记录；仅保留旧文档来源身份和作者参数，不再参与地形改写。</summary>
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

        public ITerrainGenerationModifier Freeze() => new EntranceWalkwayModifier();
    }
}
