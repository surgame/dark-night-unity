using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 工作点的只读产出和占地配置。Amount 为 -1 表示无限资源；每个工作点的剩余存量、占用者与进度均属于实例。
    /// </summary>
    public sealed class WorksiteDefinition
    {
        public string Name { get; }
        public int Amount { get; }
        public int Yield { get; }
        public double Interval { get; }
        public float Width { get; }

        public WorksiteDefinition(
            string name,
            int amount,
            int yield,
            double interval,
            float width)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Amount = amount;
            Yield = yield;
            Interval = interval;
            Width = width;
        }
    }
}
