using System;

namespace DarkNights.Core.Config.MapModule
{
    /// <summary>
    /// 模块轮廓的冻结二维点，以底层格边为单位、Y向上；不依赖Unity或运行状态。
    /// 作者输入为整数格边，裁剪期间允许有限的分数坐标，不进行像素采样近似。
    /// </summary>
    public readonly struct MapModuleVertex
    {
        public double X { get; }
        public double Y { get; }
        public MapModuleVertex(double x, double y)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
                throw new ArgumentException("模块轮廓点必须有限。");
            X = x; Y = y;
        }
    }
}
