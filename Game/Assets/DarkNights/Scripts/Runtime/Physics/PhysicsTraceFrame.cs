using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Physics;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 一次已提交物理步的独立诊断值；冻结输入、步长、地图revision和完整关联物体结果。
    /// 仅供排障，不能冒充Unity内部接触缓存或跨平台确定性重演证据。
    /// </summary>
    public sealed class PhysicsTraceFrame
    {
        public PhysicsWorldSnapshot Result { get; }
        public double DeltaSeconds { get; }
        public IReadOnlyList<ActorMotionInput> Inputs { get; }
        public IReadOnlyList<PhysicsContactSample> Contacts { get; }
        public bool ContactsTruncated { get; }
        public PhysicsTraceFrame(PhysicsWorldSnapshot result, double delta, IReadOnlyList<ActorMotionInput> inputs,
            IReadOnlyList<PhysicsContactSample> contacts, bool truncated)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            DeltaSeconds = delta;
            Inputs = Array.AsReadOnly(inputs.ToArray());
            Contacts = Array.AsReadOnly(contacts.ToArray()); ContactsTruncated = truncated;
        }
    }
}
