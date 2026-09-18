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
        public double Y { get; }
        public int WorkerId { get; }
        public int Amount { get; }
        public double Progress { get; }
        public int Variant { get; }
        public int FarmId { get; }
        public bool IsMineralDeposit { get; }
        public string RoomKind { get; }
        public string Rarity { get; }
        public int Capacity { get; }
        public string Stage { get; }
        public int DrillId { get; }

        public WorksiteSnapshot(
            int id,
            string kind,
            double x,
            int workerId,
            int amount,
            double progress,
            int variant,
            int farmId)
            : this(id, kind, x, 0, workerId, amount, progress, variant, farmId, false, "", "", 0, "", 0)
        {
        }

        public WorksiteSnapshot(
            int id,
            string kind,
            double x,
            double y,
            int workerId,
            int amount,
            double progress,
            int variant,
            int farmId,
            bool isMineralDeposit,
            string roomKind,
            string rarity,
            int capacity,
            string stage,
            int drillId)
        {
            Id = id;
            Kind = kind;
            X = x;
            Y = y;
            WorkerId = workerId;
            Amount = amount;
            Progress = progress;
            Variant = variant;
            FarmId = farmId;
            IsMineralDeposit = isMineralDeposit;
            RoomKind = roomKind ?? "";
            Rarity = rarity ?? "";
            Capacity = capacity;
            Stage = stage ?? "";
            DrillId = drillId;
        }

        public WorksiteSnapshot(
            int id,
            string kind,
            double x,
            int workerId,
            int amount,
            double progress,
            int variant,
            int farmId,
            bool isMineralDeposit,
            string roomKind,
            string rarity,
            int capacity,
            string stage,
            int drillId)
            : this(id, kind, x, 0, workerId, amount, progress, variant, farmId, isMineralDeposit,
                roomKind, rarity, capacity, stage, drillId)
        {
        }
    }
}
