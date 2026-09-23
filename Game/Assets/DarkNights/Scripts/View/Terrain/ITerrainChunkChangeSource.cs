using System;
using System.Collections.Generic;
using AnyRules.Next;
using AnyRules.Next.Unity;

namespace DarkNights.View.Terrain
{
    /// <summary>可变地形输入源在数据提交后报告受影响区块；预览据此复用同一套 AnyRuleD 局部重载流程。</summary>
    public interface ITerrainChunkChangeSource : IMapChunkSource
    {
        event Action<IReadOnlyList<ChunkCoord>> Changed;
    }
}
