using System;

namespace DarkNights.Core.ViewData
{
    /// <summary>一颗混合碎岩的冻结初始参数；规定大小、纹理、出生批次及姿态，接触轨迹另由有限烘焙计算。它不拥有地图、耐久或伤害，也不消费业务随机数。</summary>
    public readonly struct RubbleFragmentViewData
    {
        public readonly FallingRockViewData Motion;
        public readonly float Width, Height, Angle;
        public readonly int Variant;

        public RubbleFragmentViewData(FallingRockViewData motion, float width, float height, float angle, int variant)
        { Motion = motion; Width = width; Height = height; Angle = angle; Variant = variant; }

        public static RubbleFragmentViewData[] Create(FallingRockViewData[] cells)
        {
            if (cells == null || cells.Length == 0 || cells.Length > 24)
                throw new ArgumentException("混合碎岩必须绑定有限的清障区域。");
            int count = Math.Min(64, cells.Length * 3 - 1);
            var result = new RubbleFragmentViewData[count];
            double spacing = cells.Length > 1 ? Math.Max(0, cells[1].Delay - cells[0].Delay) : .045;
            for (int i = 0; i < count; i++)
            {
                // 9格样板为4大、12中、10小；每个区域至少有一颗中粒，细粒只作层次。
                bool large = cells.Length >= 9 && i < 4;
                bool small = i >= Math.Min(count, cells.Length + 7);
                int region = large ? (i + 1) % cells.Length : (i - (cells.Length >= 9 ? 4 : 0) + cells.Length) % cells.Length;
                var target = cells[region]; var source = cells[(i * 5 + 2) % cells.Length];
                float width = large ? 18 + i % 3 * 2 : small ? 4 + i % 5 : 11 + i % 5;
                float height = width * (i % 4 == 0 ? .8f : 1);
                float dx = small ? i % 7 - 3 : i % 3 - 1;
                float dy = small ? i % 5 - 4 : large ? -1 : i % 3 - 1;
                var motion = new FallingRockViewData(source.SourceX + i % 5 - 2, source.SourceY + i % 3 - 1,
                    target.LandingX + dx, target.LandingY + dy, source.Gravity,
                    i % 3 * spacing * 8 / 3 + i / 3 * spacing * 11 / 9);
                result[i] = new RubbleFragmentViewData(motion, width, height, (i % 7 - 3) * 7, i % 4);
            }
            return result;
        }
    }
}
