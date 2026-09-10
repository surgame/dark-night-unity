using System;
using System.Collections.Generic;

namespace DarkNights.Samples.LanCoop.Bootstrap
{
    /// <summary>独立 Player 的可机读观察记录；只记录实际状态和回执，成功判定由外部断言脚本完成。</summary>
    [Serializable]
    public sealed class SampleReport
    {
        public int processId, joinCount, epoch, revision, coins, purchases, occupant, ticks, inputId, senderCount;
        public bool ready, paused;
        public string role, status, error, unityVersion, scriptingBackend;
        public List<string> results = new List<string>();
    }
}
