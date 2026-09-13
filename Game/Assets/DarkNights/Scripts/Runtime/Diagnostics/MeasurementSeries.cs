using System;

namespace DarkNights.Runtime.Diagnostics
{
    /// <summary>
    /// 显式验收模式的有界全程直方图，保留每个样本的计数、总和和最大值，不丢弃早期观测。
    /// 正分位数返回桶上界，相对误差不超过 1%；零值精确计数，极小值桶宽为 0.000001。
    /// 不参与玩法或网络决策；固定内存与观测时长无关，重置仅由显式验收入口触发。
    /// </summary>
    public sealed class MeasurementSeries
    {
        private const double Minimum = 0.000001;
        private const double Ratio = 1.01;
        private static readonly double LogRatio = Math.Log(Ratio);
        private static readonly double LogMinimum = Math.Log(Minimum);
        private readonly long[] buckets = new long[8194];
        public long Count { get; private set; }
        public long RetainedCount => Count;
        public double Total { get; private set; }
        public double Maximum { get; private set; }
        public const string QuantileMethod = "Full-history histogram; upper-bound quantiles, <=1% relative error above 0.000001";

        public void Add(double value)
        {
            if (value < 0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            int index = value == 0 ? 0 : value <= Minimum ? 1 : 1 + (int)Math.Ceiling((Math.Log(value) - LogMinimum) / LogRatio);
            if (index >= buckets.Length) throw new ArgumentOutOfRangeException(nameof(value), "Measurement exceeds histogram range.");
            buckets[index]++;
            Count++;
            Total += value;
            Maximum = Math.Max(Maximum, value);
        }

        public double Percentile(double quantile)
        {
            if (quantile <= 0 || quantile > 1 || double.IsNaN(quantile)) throw new ArgumentOutOfRangeException(nameof(quantile));
            if (Count == 0) return 0;
            long required = (long)Math.Ceiling(Count * quantile), seen = 0;
            for (int i = 0; i < buckets.Length; i++)
            {
                seen += buckets[i];
                if (seen >= required) return i == 0 ? 0 : Math.Min(Maximum, Minimum * Math.Pow(Ratio, i - 1));
            }
            return Maximum;
        }

        public void Clear()
        {
            Array.Clear(buckets, 0, buckets.Length);
            Count = 0;
            Total = Maximum = 0;
        }
    }
}
