using System;

namespace DarkNights.Runtime.Diagnostics
{
    /// <summary>
    /// 显式验收模式的有界数值采样，保留最近十万个样本；只有导出时排序副本。
    /// 不参与规则、时钟或网络决策，采样过程不分配数组，完整计数与总和不受保留窗口影响。
    /// </summary>
    public sealed class MeasurementSeries
    {
        private readonly double[] values = new double[100000];
        public long Count { get; private set; }
        public int RetainedCount => (int)Math.Min(Count, values.Length);
        public double Total { get; private set; }
        public double Maximum { get; private set; }

        public void Add(double value)
        {
            values[(int)(Count % values.Length)] = value;
            Count++;
            Total += value;
            Maximum = Math.Max(Maximum, value);
        }

        public double Percentile(double quantile)
        {
            if (quantile <= 0 || quantile > 1 || double.IsNaN(quantile)) throw new ArgumentOutOfRangeException(nameof(quantile));
            int length = RetainedCount;
            if (length == 0) return 0;
            var sorted = new double[length];
            Array.Copy(values, sorted, length);
            Array.Sort(sorted);
            return sorted[Math.Min(length - 1, (int)Math.Ceiling(length * quantile) - 1)];
        }
    }
}
