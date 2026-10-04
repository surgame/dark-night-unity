using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>矿层最终格子的冻结保存合同；零类型明确表示耗尽空格，集合按地图行序复制，不拥有运行状态。</summary>
    public sealed class MineralMapSnapshot
    {
        private readonly byte[] kinds;
        private readonly int[] durability, reserves;
        public string RulesFingerprint { get; }
        public int Count => kinds.Length;
        public MineralMapSnapshot(byte[] kinds, int[] durability, int[] reserves, string fingerprint)
        {
            if (kinds == null || durability == null || reserves == null ||
                kinds.Length != TerrainGenerationSettings.Width * TerrainGenerationSettings.Height ||
                durability.Length != kinds.Length || reserves.Length != kinds.Length || fingerprint == null || fingerprint.Length != 64)
                throw new ArgumentException("矿层保存尺寸或规则指纹无效。");
            for (int i = 0; i < kinds.Length; i++)
                if (kinds[i] > 5 || durability[i] < 0 || durability[i] > 1000000 || reserves[i] < 0 || reserves[i] > 1000000 ||
                    (kinds[i] == 0 ? durability[i] != 0 || reserves[i] != 0 : durability[i] == 0 || reserves[i] == 0))
                    throw new ArgumentException("矿层保存含非法耐久、存量或空格状态。");
            this.kinds = (byte[])kinds.Clone(); this.durability = (int[])durability.Clone(); this.reserves = (int[])reserves.Clone();
            RulesFingerprint = fingerprint;
        }
        public byte Kind(int index) => kinds[index];
        public int Durability(int index) => durability[index];
        public int Reserves(int index) => reserves[index];
    }
}
