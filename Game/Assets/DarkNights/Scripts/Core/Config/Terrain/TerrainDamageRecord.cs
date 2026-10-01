using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>格子业务偏差的冻结保存合同；以稳定材质 GUID 绑定最终格，恢复时重新绑定世界代次，不拥有运行状态。</summary>
    public sealed class TerrainDamageRecord
    {
        public int U { get; }
        public int V { get; }
        public string MaterialGuid { get; }
        public int Durability { get; }
        public int Quality { get; }
        public int Reserves { get; }
        public int Blocking { get; }
        public string Occupant { get; }
        public TerrainDamageRecord(int u, int v, string materialGuid, int durability, int quality, int reserves, int blocking, string occupant)
        {
            if (u < 0 || u >= TerrainGenerationSettings.Width || v > 0 || v <= -TerrainGenerationSettings.Height ||
                !Guid.TryParseExact(materialGuid, "N", out _) || durability < 1 || quality < 0 || quality > ushort.MaxValue ||
                reserves < 0 || blocking < 0 || blocking > 2 || !string.IsNullOrEmpty(occupant) && !Guid.TryParseExact(occupant, "N", out _))
                throw new ArgumentException("格业务保存记录无效。");
            U = u; V = v; MaterialGuid = materialGuid; Durability = durability;
            Quality = quality; Reserves = reserves; Blocking = blocking; Occupant = occupant ?? "";
        }
    }
}
