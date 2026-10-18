using System;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Save;
using MemoryPack;
using Newtonsoft.Json.Linq;

namespace DarkNights.Runtime.Network
{
    /// <summary>可靠完整投影中的单个地质事件传输值；接收立即解析有界静态清单并冻结，绝不持有Host池状态或自行推进事件。</summary>
    [MemoryPackable]
    public partial class GeologyWire
    {
        public string Site { get; set; }
        public int Phase { get; set; }
        public double Clock { get; set; }
        public string Reason { get; set; }
        public uint SoilMask { get; set; }
        public static GeologyWire From(GeologySiteViewData value) => new GeologyWire
        { Site = TerrainGeologySaveJson.Site(value.Site).ToString(Newtonsoft.Json.Formatting.None), Phase = value.State.Phase,
            Clock = value.State.Clock, Reason = value.State.Reason, SoilMask = value.State.SoilMask };
        public GeologySiteViewData Freeze()
        {
            if (Site == null || Site.Length > 10000) throw new FormatException("塌方静态投影超限。");
            var site = TerrainGeologySaveJson.ReadSite(JObject.Parse(Site, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }));
            var config = Objects.GeologySiteConfiguration.Resolve(site);
            if ((SoilMask >> config.RubbleCount) != 0) throw new FormatException("塌方占格位图超限。");
            return new GeologySiteViewData(site, new GeologyEventViewData(Phase, Clock, Reason, SoilMask));
        }
    }
}
