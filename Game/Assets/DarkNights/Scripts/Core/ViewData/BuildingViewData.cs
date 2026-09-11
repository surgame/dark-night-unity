using System;
using System.Collections.Generic;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 建筑的一次冻结展示值；训练项本身不可变，构造时再复制队列，异步视图不会观察到权威队列增删。
    /// 生命和进度只由后续完整投影替换，不在本对象推进施工或攻击。
    /// </summary>
    public sealed class BuildingViewData
    {
        public int Id { get; }
        public string Kind { get; }
        public float X { get; }
        public double Hp { get; }
        public double Progress { get; }
        public int WorkerId { get; }
        public int FarmSiteId { get; }
        public double HitFlash { get; }
        public IReadOnlyList<TrainingViewData> Training { get; }

        public BuildingViewData(int id, string kind, float x, double hp, double progress,
            int workerId, int farmSiteId, double hitFlash, IReadOnlyList<TrainingViewData> training)
        {
            if (training == null) throw new ArgumentNullException(nameof(training));
            if (training.Count > WorldViewData.MaximumEntities) throw new ArgumentOutOfRangeException(nameof(training));
            var copy = new List<TrainingViewData>(training.Count);
            foreach (var item in training) copy.Add(item ?? throw new ArgumentException("Null training item."));
            Id = id;
            Kind = kind;
            X = x;
            Hp = hp;
            Progress = progress;
            WorkerId = workerId;
            FarmSiteId = farmSiteId;
            HitFlash = hitFlash;
            Training = copy.AsReadOnly();
        }
    }
}
