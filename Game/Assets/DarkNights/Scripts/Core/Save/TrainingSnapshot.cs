using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 兵营队列的一条存档记录，描述已支付的目标职业和剩余模拟秒数。读取时验证工人身份及唯一的兵营归属。
    /// </summary>
    public sealed class TrainingSnapshot
    {
        public int ActorId { get; }
        public string Kind { get; }
        public double Remaining { get; }

        public TrainingSnapshot(
            int actorId,
            string kind,
            double remaining)
        {
            ActorId = actorId;
            Kind = kind;
            Remaining = remaining;
        }
    }
}
