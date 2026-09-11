using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 建筑生命、施工和训练队列的schema v1记录。农田的工作点身份由WorksiteSnapshot中的反向关联恢复。
    /// </summary>
    public sealed class BuildingSnapshot
    {
        public int Id { get; }
        public string Kind { get; }
        public double X { get; }
        public double Hp { get; }
        public double Progress { get; }
        public int WorkerId { get; }
        public double AttackClock { get; }
        public IReadOnlyList<TrainingSnapshot> TrainingQueue { get; }

        public BuildingSnapshot(
            int id,
            string kind,
            double x,
            double hp,
            double progress,
            int workerId,
            double attackClock,
            IReadOnlyList<TrainingSnapshot> trainingQueue)
        {
            Id = id;
            Kind = kind;
            X = x;
            Hp = hp;
            Progress = progress;
            WorkerId = workerId;
            AttackClock = attackClock;
            TrainingQueue = trainingQueue == null ? null : new List<TrainingSnapshot>(trainingQueue).AsReadOnly();
        }
    }
}
