using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Core.Logic.Entities
{
    /// <summary>
    /// 权威模拟拥有的付费训练记录；训练计时只由所属兵营推进，职业切换保持原实体身份。
    /// </summary>
    public sealed class TrainingOrder
    {
        public int ActorId { get; }
        public string Kind { get; }
        public double Remaining { get; internal set; }

        public TrainingOrder(int ActorId, string Kind, double Remaining)
        {
            this.ActorId = ActorId;
            this.Kind = Kind;
            this.Remaining = Remaining;
        }
    }
}
