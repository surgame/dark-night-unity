using System;
using DarkNights.Runtime.Diagnostics;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>
    /// 防止长时间验收只统计尾部样本，并核验有界直方图的零值、精度与重置边界。
    /// 不设置硬件耗时门槛；分位数允许明确声明的桶上界误差，不能丢失早期负载。
    /// </summary>
    public sealed class MeasurementSeriesTests
    {
        [Test]
        public void LongObservationKeepsEarlyLoadInQuantiles()
        {
            var series = new MeasurementSeries();
            for (int i = 0; i < 110000; i++) series.Add(50);
            for (int i = 0; i < 90000; i++) series.Add(1);
            Assert.That(series.Count, Is.EqualTo(200000));
            Assert.That(series.RetainedCount, Is.EqualTo(series.Count));
            Assert.That(series.Percentile(.5), Is.InRange(50, 50.5));
            Assert.That(series.Percentile(.95), Is.InRange(50, 50.5));
            Assert.That(series.Total, Is.EqualTo(5590000));
        }

        [Test]
        public void QuantilesHaveBoundedErrorAcrossTimeAndByteScales()
        {
            foreach (double value in new[] { .00001, .21, 1.7, 15.5, 255.4, 12345678.0 })
            {
                var series = new MeasurementSeries();
                for (int i = 0; i < 100; i++) series.Add(value);
                series.Add(value * 100);
                Assert.That(series.Percentile(.95), Is.InRange(value, value * 1.01));
                Assert.That(series.Percentile(1), Is.EqualTo(value * 100));
            }
        }

        [Test]
        public void ResetAndZeroSamplesDoNotCarryEarlierMeasurements()
        {
            var series = new MeasurementSeries();
            series.Add(123);
            series.Clear();
            series.Add(0);
            Assert.That(series.Count, Is.EqualTo(1));
            Assert.That(series.Total, Is.Zero);
            Assert.That(series.Maximum, Is.Zero);
            Assert.That(series.Percentile(.95), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => series.Add(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => series.Add(-1));
        }
    }
}
