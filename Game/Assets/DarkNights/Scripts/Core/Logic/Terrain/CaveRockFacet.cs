namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>单个确定性岩块的采样中心与基色级别；只在一次纯计算烘焙中存活，不是游戏对象或持久状态。</summary>
    internal readonly struct CaveRockFacet
    {
        public readonly double X, Y;
        public readonly int Tone;
        public CaveRockFacet(double x, double y, int tone) { X = x; Y = y; Tone = tone; }
    }
}
