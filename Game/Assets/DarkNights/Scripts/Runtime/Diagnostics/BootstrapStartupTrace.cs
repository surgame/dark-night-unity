using System;
using System.Diagnostics;

namespace DarkNights.Runtime.Diagnostics
{
    /// <summary>
    /// 为显式启动采集提供无状态阶段通知；不拥有资源、Ready或业务生命周期。
    /// 仅Editor／开发构建调用，未订阅时不记录、不写盘，也不改变启动依赖。
    /// </summary>
    public static class BootstrapStartupTrace
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static event Action<string> StageRecorded;
#endif

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Mark(string stage)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            StageRecorded?.Invoke(stage);
#endif
        }
    }
}
