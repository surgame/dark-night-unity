namespace DarkNights.Core.Config.Terrain
{
    /// <summary>冻结的洞室连接及掩埋位置；几何生成先连通再覆盖，运行变化不回写此记录。</summary>
    public sealed class CavePassage
    {
        public int From { get; }
        public int To { get; }
        public CavePassageKind Kind { get; }
        public int BendX { get; }
        public int BendY { get; }
        public int Radius { get; }
        public int CoverLength { get; }
        public CavePassage(int from, int to, CavePassageKind kind, int bendX, int bendY, int radius, int coverLength)
        {
            From = from; To = to; Kind = kind; BendX = bendX; BendY = bendY;
            Radius = radius; CoverLength = coverLength;
        }
    }
}
