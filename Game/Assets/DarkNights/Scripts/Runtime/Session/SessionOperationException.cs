using System;

namespace DarkNights.Runtime.Session
{
    /// <summary>可恢复的请求执行拒绝；只在事务已经取消且当前营地未改变时返回给权威队列，不表示会话故障。</summary>
    public sealed class SessionOperationException : InvalidOperationException
    {
        public SessionResultCode Code { get; }
        public SessionOperationException(SessionResultCode code, string message, Exception cause) : base(message, cause)
        {
            Code = code;
        }
    }
}
