using System;

namespace DarkNights.View.MapModule
{
    /// <summary>Scene内两个模块实例的显式端口连接；只引用作者对象，不拥有连接求解器或运行状态。</summary>
    [Serializable]
    public sealed class MapModuleConnection
    {
        public MapModuleAuthoring From;
        public string FromPort;
        public MapModuleAuthoring To;
        public string ToPort;
    }
}
