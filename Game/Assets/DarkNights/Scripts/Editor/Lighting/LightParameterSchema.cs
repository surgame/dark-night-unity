using System;
using DarkNights.Core.Config;

namespace DarkNights.Editor.Lighting
{
    /// <summary>单灯制作字段的有限编辑器描述；工坊与调试台共用标签、范围和模式可见性，隐藏不修改作者值，冻结值验证仍由正式配置合同负责。</summary>
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
            ("FillIntensity", "对象补光强度", 0, 2), ("FillColor", "对象补光颜色", 0, 0),
            ("BounceMode", "墙面返光模式", 0, 0),
            ("Bounce", "单点返光强度（LegacyPoint）", 0, .4f),
            ("DiffuseShape", "漫反射光形（沿墙／柔光团）", 0, 0),
            ("DiffuseOcclusionMode", "返光遮挡规则（⑨）", 0, 0),
            ("DiffuseFeatherWidth", "切边向外羽化宽度（格）", 0, 4),
            ("DiffuseMatchIncident", "匹配主光入射亮度", 0, 0),
            ("DiffuseCornerRadius", "相邻墙面连接半径（格，0关闭）", 0, 2),
            ("DiffuseGain", "墙面漫反射增益", 0, 2),
            ("DiffuseNearDistance", "漫反射近距离（手电距墙，格）", 0, 6),
            ("DiffuseFarDistance", "漫反射关闭距离（手电距墙，格）", .1f, 14),
            ("DiffuseMaxReach", "漫反射最大范围／法向深度（格）", 0, 6),
            ("DiffuseLateralSpread", "漫反射沿墙扩展（WallBand）", 0, 2),
            ("DiffuseVisibilityProbes", "每墙段遮挡探针", 2, 4)
        };

        public static bool IsVisible(string field, LightBounceMode mode, WallDiffuseShape shape,
            WallDiffuseOcclusionMode occlusion = WallDiffuseOcclusionMode.ProjectedShadow)
        {
            if (field == "Bounce") return mode == LightBounceMode.LegacyPoint;
            if (!field.StartsWith("Diffuse", StringComparison.Ordinal)) return true;
            return mode == LightBounceMode.Continuous &&
                (field != "DiffuseLateralSpread" || shape == WallDiffuseShape.WallBand) &&
                (field != "DiffuseFeatherWidth" || occlusion == WallDiffuseOcclusionMode.OutwardFeather) &&
                (field != "DiffuseVisibilityProbes" || occlusion != WallDiffuseOcclusionMode.WallMask);
        }

        public static string Label(string field, string fallback, WallDiffuseShape shape)
        {
            if (field != "DiffuseMaxReach") return fallback;
            return shape == WallDiffuseShape.SoftBlob ? "漫反射光团最大半径（格）" : "漫反射最大法向深度（格）";
        }

        public static string Hint(string field)
        {
            switch (field)
            {
                case "DiffuseOcclusionMode": return "原始切边沿用路径遮挡；墙体遮罩只裁岩体中的返光，可能照亮隔墙空气；向外羽化只放宽墙段端部切边。仅 PrivateField／Continuous 生效。";
                case "DiffuseFeatherWidth": return "在⑨内向阴影侧扩展，不改变圆形半径；默认1.5格，0恢复原切边，最大4格。仍检查墙角至目标的空气路径，不增加探针数或后处理Pass。";
                case "DiffuseVisibilityProbes": return "原始切边与向外羽化使用2—4探针；墙体遮罩模式不查询返光路径，隐藏时保留数值。";
                case "DiffuseCornerRadius": return "两种光形均有效：控制相邻受光墙边分组；WallBand 还用它柔化相连端部。";
                case "DiffuseMaxReach": return "WallBand 控制离墙深度，SoftBlob 控制径向扩散半径；实际范围还随手电距墙衰减。";
                case "DiffuseNearDistance":
                case "DiffuseFarDistance": return "控制手电距受光墙面的距离门控，不是返光从墙面向外传播的衰减距离。";
                default: return "";
            }
        }

        public static LightOverrideMask Mask(string field) => (LightOverrideMask)Enum.Parse(typeof(LightOverrideMask), field);

        public static string Group(string field)
        {
            if (field == "Bounce" || field == "BounceMode" || field.StartsWith("Diffuse", StringComparison.Ordinal)) return "bounce";
            if (field == "LocalFillEnabled" || field.StartsWith("Fill", StringComparison.Ordinal) || field.StartsWith("Near", StringComparison.Ordinal)) return "fill";
            return "beam";
        }
        public static string Section(string field)
        {
            if (field == "DiffuseVisibilityProbes") return "高级采样";
            if (field == "DiffuseOcclusionMode" || field == "DiffuseFeatherWidth") return "遮挡与切边";
            if (field == "DiffuseMaxReach" || field == "DiffuseCornerRadius" || field == "DiffuseNearDistance" || field == "DiffuseFarDistance" || field == "DiffuseLateralSpread") return "空间与距离门控";
            if (Group(field) == "bounce") return "模式与亮度";
            if (field.StartsWith("Near", StringComparison.Ordinal)) return "环境散光";
            if (Group(field) == "fill") return "指定对象补光";
            if (field == "SoftShadows" || field == "Softness" || field == "ConeFeather" || field == "ApertureWidth") return "柔影与光束边缘";
            return "主光与发光";
        }
    }
}
