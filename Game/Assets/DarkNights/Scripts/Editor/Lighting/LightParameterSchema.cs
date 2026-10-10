using System;
using DarkNights.Core.Config;

namespace DarkNights.Editor.Lighting
{
    /// <summary>单灯制作字段的有限编辑器描述；Definition 工坊与 UI Toolkit 调试台共用标签及范围，冻结值验证仍由正式配置合同负责。</summary>
    public static class LightParameterSchema
    {
        public static readonly (string Name, string Label, float Low, float High)[] Fields =
        {
            ("EnvironmentEnabled", "环境照明", 0, 0), ("LocalFillEnabled", "指定对象补光", 0, 0),
            ("Directional", "定向光束", 0, 0), ("Range", "照射距离（格）", 2, 24),
            ("Cone", "光束角度（度）", 20, 150), ("ApertureWidth", "灯口宽度（格）", 0, 2),
            ("Intensity", "直射强度", 0, 4), ("Color", "光源颜色", 0, 0),
            ("SoftShadows", "柔光遮挡", 0, 0), ("Softness", "阴影柔度（格）", 0, .75f),
            ("ConeFeather", "光束边缘", 0, .25f), ("NearRange", "环境散光半径（格）", .25f, 5),
            ("NearIntensity", "环境散光强度", 0, 2), ("FillRadius", "对象补光半径（格）", .25f, 5),
            ("FillIntensity", "对象补光强度", 0, 2), ("FillColor", "对象补光颜色", 0, 0)
        };

        public static LightOverrideMask Mask(string field) => (LightOverrideMask)Enum.Parse(typeof(LightOverrideMask), field);
    }
}
