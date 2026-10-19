using System;

namespace DarkNights.Core.Config.Expedition
{
    /// <summary>
    /// 星图节点的冻结作者资料；坐标属于示意宇宙，不是地面泊位。
    /// 危险分档仅供导航展示，示意节点不得据此创建未审核星球或改变运行危险。
    /// </summary>
    public sealed class NavigationNodeDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public float X { get; }
        public float Y { get; }
        public int Danger { get; }
        public string Risk { get; }
        public bool PreviewOnly { get; }

        public NavigationNodeDefinition(string id, string name, float x, float y,
            int danger, string risk, bool previewOnly)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 48 || id != id.Trim() ||
                string.IsNullOrWhiteSpace(name) || name.Length > 64 || risk == null || risk.Length > 160)
                throw new ArgumentException("导航节点身份、名称或危险描述无效。");
            foreach (char c in id)
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-' && c != '_')
                    throw new ArgumentException("导航节点身份须为小写字母、数字、横线或下划线。");
            ValidateCoordinate(x); ValidateCoordinate(y);
            if (danger < 1 || danger > 3) throw new ArgumentOutOfRangeException(nameof(danger));
            Id = id; Name = name; X = x; Y = y; Danger = danger; Risk = risk; PreviewOnly = previewOnly;
        }

        public static void ValidateCoordinate(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 4096)
                throw new ArgumentOutOfRangeException(nameof(value), "星图坐标须为有界有限数。");
        }
    }
}
