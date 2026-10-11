using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>从只读 H5 基线追溯的矿簇锚点；仅冻结位置、矿种与初始容量，正式矿层负责最终分配和采空状态。</summary>
    [Serializable]
    public sealed class MineMapOre
    {
        public string Id;
        public string Kind;
        public int X;
        public int Y;
        public int Capacity;
        public bool Buried;
        public MineMapOre Copy() => (MineMapOre)MemberwiseClone();
    }
}
