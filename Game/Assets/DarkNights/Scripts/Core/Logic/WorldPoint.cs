namespace DarkNights.Core.Logic
{
    /// <summary>
    /// 箭矢使用的只读二维规则坐标，保持原游戏单精度分量。没有引擎向量依赖，也不包含显示插值或物理行为。
    /// </summary>
    public readonly struct WorldPoint
    {
        public float X { get; }
        public float Y { get; }

        public WorldPoint(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
