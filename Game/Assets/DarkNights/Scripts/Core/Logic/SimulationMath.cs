using System;

namespace DarkNights.Core.Logic
{
    /// <summary>
    /// 仅实现规则实际使用的移动和格点吸附，保留原 double 运算后转 float 的边界；不承担通用引擎数学适配。
    /// </summary>
    internal static class SimulationMath
    {
        public static double MoveToward(double from, double to, double delta) =>
            Math.Abs(to - from) <= delta ? to : from + Math.Sign(to - from) * delta;

        public static double Snapped(double value, double step) =>
            step == 0 ? value : Math.Floor(value / step + 0.5) * step;
    }
}
