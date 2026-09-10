using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 营地经济的只读配置，保存开局库存、维护、招募、修缮与训练参数。实际库存和所有计时器属于每局权威状态。
    /// </summary>
    public sealed class EconomyDefinition
    {
        public ResourceAmounts StartingResources { get; }
        public double UpkeepInterval { get; }
        public double FoodPerPerson { get; }
        public double StarvationInterval { get; }
        public ResourceAmounts RecruitCost { get; }
        public double RecruitSeconds { get; }
        public ResourceAmounts RepairCost { get; }
        public double RepairHp { get; }
        public double TrainingSeconds { get; }
        public int TrainingQueueLimit { get; }

        public EconomyDefinition(
            ResourceAmounts startingResources,
            double upkeepInterval,
            double foodPerPerson,
            double starvationInterval,
            ResourceAmounts recruitCost,
            double recruitSeconds,
            ResourceAmounts repairCost,
            double repairHp,
            double trainingSeconds,
            int trainingQueueLimit)
        {
            StartingResources = startingResources ?? throw new ArgumentNullException(nameof(startingResources));
            UpkeepInterval = upkeepInterval;
            FoodPerPerson = foodPerPerson;
            StarvationInterval = starvationInterval;
            RecruitCost = recruitCost ?? throw new ArgumentNullException(nameof(recruitCost));
            RecruitSeconds = recruitSeconds;
            RepairCost = repairCost ?? throw new ArgumentNullException(nameof(repairCost));
            RepairHp = repairHp;
            TrainingSeconds = trainingSeconds;
            TrainingQueueLimit = trainingQueueLimit;
        }
    }
}
