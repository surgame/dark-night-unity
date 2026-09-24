using System;
using System.Collections.Generic;
using AnyRules.Next;
using AnyRules.Next.Unity;

namespace DarkNights.View.Terrain
{
    /// <summary>可变只读表现输入源在事务提交后发布冻结的格变化；网络与编辑器共用同一原位安装合同。</summary>
    public interface ITerrainInputSource : IMapChunkSource
    {
        event Action<MapInputBatch> InputChanged;
        /// <summary>装载地图后发布当前已加载区块的完整输入基线，并建立后续增量的源游标。</summary>
        void PublishInitialBaseline();
    }
}
