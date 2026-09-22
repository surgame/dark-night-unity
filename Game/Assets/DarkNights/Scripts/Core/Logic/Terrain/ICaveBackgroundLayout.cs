namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>背景生成器交付的只读三槽轮廓；近／中／深槽的着色、分页与缓存由消费者复用，生成器不接触 Unity 或权威地图。</summary>
    public interface ICaveBackgroundLayout
    {
        int Width { get; }
        int Height { get; }
        int Top { get; }
        uint LayoutSeed { get; }
        bool Solid(int layer, int x, int y);
    }
}
