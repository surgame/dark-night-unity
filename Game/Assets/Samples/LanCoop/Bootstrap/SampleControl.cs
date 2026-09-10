using System;

namespace DarkNights.Samples.LanCoop.Bootstrap
{
    /// <summary>仅本机自动化文件的控制格式；id 单调递增，文件入口不属于远程协议或正式 Player 功能。</summary>
    [Serializable]
    public sealed class SampleControl
    {
        public int id, entity = 1, fault;
        public string action;
        public bool repeat;
    }
}
