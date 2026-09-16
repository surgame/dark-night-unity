using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 从传输回调复制出来的不可变业务参数，不含玩家身份、资源数量或伤害。
    /// 列表有硬上限并按首次出现保序去重；缓存比较规范化后的完整参数，不能保留池化命令引用。
    /// </summary>
    public sealed class SessionRequest
    {
        public const int MaximumActors = 256;
        public SessionOperation Operation { get; }
        public int Protocol { get; }
        public int Epoch { get; }
        public int PolicyRevision { get; }
        public long Sequence { get; }
        public IReadOnlyList<int> ActorIds { get; }
        public int TargetId { get; }
        public float X { get; }
        public string Kind { get; }
        public int Value { get; }
        public int ControlLease { get; }

        public SessionRequest(SessionOperation operation, int protocol, int epoch, int policyRevision,
            long sequence, IReadOnlyList<int> actorIds = null, int targetId = 0, float x = 0,
            string kind = "", int value = 0, int controlLease = 0)
        {
            if (actorIds != null && actorIds.Count > MaximumActors)
                throw new ArgumentException("Too many actors.", nameof(actorIds));
            if (kind == null || kind.Length > 64) throw new ArgumentException("Invalid kind.", nameof(kind));
            Operation = operation;
            Protocol = protocol;
            Epoch = epoch;
            PolicyRevision = policyRevision;
            Sequence = sequence;
            var seen = new HashSet<int>();
            var ordered = new List<int>();
            foreach (int id in actorIds ?? Array.Empty<int>())
                if (seen.Add(id)) ordered.Add(id);
            ActorIds = ordered.AsReadOnly();
            TargetId = targetId;
            X = x;
            Kind = kind;
            Value = value;
            ControlLease = controlLease;
        }

        internal bool SameIntent(SessionRequest other) => Operation == other.Operation && Protocol == other.Protocol &&
            Epoch == other.Epoch && PolicyRevision == other.PolicyRevision && Sequence == other.Sequence &&
            ActorIds.SequenceEqual(other.ActorIds) && TargetId == other.TargetId && X.Equals(other.X) &&
            Kind == other.Kind && Value == other.Value && ControlLease == other.ControlLease;
    }
}
