using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 工位存量、进度和占用者的传输记录。农田ID必须指向同坐标的已完成农田，有限资源不能使用负存量。
    /// </summary>
    public sealed class WorksiteSnapshot
    {
        public int Id { get; }
        public string Kind { get; }
        public double X { get; }
        public int WorkerId { get; }
        public int Amount { get; }
        public double Progress { get; }
        public int Variant { get; }
        public int FarmId { get; }

        public WorksiteSnapshot(
            int id,
            string kind,
            double x,
            int workerId,
            int amount,
            double progress,
            int variant,
            int farmId)
        {
            Id = id;
            Kind = kind;
            X = x;
            WorkerId = workerId;
            Amount = amount;
            Progress = progress;
            Variant = variant;
            FarmId = farmId;
        }
    }
}
