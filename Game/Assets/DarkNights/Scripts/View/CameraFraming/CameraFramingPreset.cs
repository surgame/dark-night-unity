using UnityEngine;

namespace DarkNights.View.CameraFraming
{
    /// <summary>
    /// 本地地面镜头的人物占屏高度方案；预设只负责填入初始百分比。
    /// Inspector手动修改占屏百分比后改用自定义，配置不参与权威状态或存档。
    /// </summary>
    public enum CameraFramingPreset
    {
        [InspectorName("自定义")]
        Custom = 0,
        [InspectorName("近景 · 16%")]
        Close16 = 1,
        [InspectorName("原1080p参照 · 12.4%")]
        Reference124 = 2,
        [InspectorName("截图相近 · 10.5%（默认）")]
        Screenshot105 = 3,
        [InspectorName("拉远 · 8%")]
        Wide8 = 4,
        [InspectorName("远景 · 6%")]
        Distant6 = 5
    }
}
