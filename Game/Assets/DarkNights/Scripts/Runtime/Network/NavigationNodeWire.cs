using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>单个导航节点的具体序列化字段；仅承载作者资料，接收方经NavigationWire冻结和限界校验。</summary>
    [MemoryPackable]
    public partial class NavigationNodeWire
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public int Danger { get; set; }
        public string Risk { get; set; }
        public bool PreviewOnly { get; set; }
    }
}
