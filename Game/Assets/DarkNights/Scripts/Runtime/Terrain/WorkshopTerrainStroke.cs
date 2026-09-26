using System.Collections.Generic;
using AnyRules.Next;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>一次工作台笔触的首值与末值，归离线撤销历史所有；只保存修改格的差异，不持有第二份运行地图。</summary>
    internal sealed class WorkshopTerrainStroke
    {
        internal readonly Dictionary<CellCoord, GridCell> Before = new Dictionary<CellCoord, GridCell>();
        internal readonly Dictionary<CellCoord, GridCell> After = new Dictionary<CellCoord, GridCell>();
    }
}
