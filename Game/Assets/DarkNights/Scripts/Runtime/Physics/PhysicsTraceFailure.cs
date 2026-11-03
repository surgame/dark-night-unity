using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Physics;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 写事务失败前冻结的本步输入与异常，保留在已提交片段之外，不能冒充成功状态。
    /// 错误文本有界，失败自动停止录制，业务回滚及原生缓存退休仍由原事务负责。
    /// </summary>
    public sealed class PhysicsTraceFailure
    {
        public double DeltaSeconds { get; }
        public IReadOnlyList<ActorMotionInput> Inputs { get; }
        public string Error { get; }
        public PhysicsTraceFailure(double delta, IReadOnlyList<ActorMotionInput> inputs, Exception error)
        {
            DeltaSeconds = delta; Inputs = Array.AsReadOnly(inputs.ToArray());
            string text = error.ToString(); Error = text.Length > 8192 ? text.Substring(0, 8192) : text;
        }
    }
}
