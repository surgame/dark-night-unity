using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.ViewData
{
    /// <summary>正式塌方的深度冻结展示值；静态站点、阶段与实际瓦砾位图属于同一个对象投影，客户端仅据此显示现有Prefab。</summary>
    public sealed class GeologySiteViewData
    {
        public TerrainGeologySite Site { get; }
        public GeologyEventViewData State { get; }
        public GeologySiteViewData(TerrainGeologySite site, GeologyEventViewData state)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            new TerrainGeologyProgress(site.SiteId, state.Phase, state.Clock, state.Reason);
            if (state.Phase != 4 && state.SoilMask != 0) throw new ArgumentException("非堆积阶段不得投影瓦砾占格。");
            State = state;
        }
    }
}
