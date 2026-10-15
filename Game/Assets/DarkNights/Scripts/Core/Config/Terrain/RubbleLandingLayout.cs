using System;
using System.Collections.Generic;
using DarkNights.Core.ViewData;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>从冻结接触结果推导瓦砾的16像素清障区域；按各列可见碎岩上沿建立保守实体包络，自底向上排列以保留支撑。只返回配置数据，不拥有可写地形。</summary>
    public static class RubbleLandingLayout
    {
        public static (int U, int V)[] Build(RubbleCollapseViewData collapse, int floorV)
        {
            var heights = new SortedDictionary<int, int>();
            for (int i = 0; i < collapse.Count; i++)
            {
                var piece = collapse.Fragment(i);
                collapse.Sample(i, collapse.Duration, out float x, out float y, out float angle);
                double radians = angle * Math.PI / 180, cos = Math.Cos(radians), sin = Math.Sin(radians);
                float halfX = (float)(Math.Abs(cos) * piece.Width + Math.Abs(sin) * piece.Height) * .5f;
                for (int pixel = (int)Math.Floor(x - halfX); pixel < (int)Math.Ceiling(x + halfX); pixel++)
                {
                    int u = (int)Math.Floor((pixel + .5 + 8) / 16);
                    float peak = collapse.FloorY;
                    // 有界像素采样沿最终旋转矩形推导上沿，不能套用固定三角形模板。
                    for (int dy = -32; dy <= 32; dy++)
                    {
                        double px = pixel + .5 - x, py = dy + .5;
                        if (Math.Abs(px * cos + py * sin) <= piece.Width * .5 &&
                            Math.Abs(-px * sin + py * cos) <= piece.Height * .5) peak = Math.Max(peak, y + dy + 1);
                    }
                    int rows = (int)Math.Ceiling((peak - collapse.FloorY) / 16);
                    if (rows <= 0) continue;
                    if (rows > 6) throw new ArgumentException("碎岩结果超出六层清障高度预算。");
                    if (!heights.TryGetValue(u, out int old) || rows > old) heights[u] = rows;
                }
            }
            var result = new List<(int U, int V)>();
            for (int row = 0; row < 6; row++)
                foreach (var pair in heights) if (pair.Value > row) result.Add((pair.Key, floorV + row));
            if (result.Count == 0 || result.Count > 31) throw new ArgumentException("碎岩结果超出31个清障区域预算：" + result.Count);
            return result.ToArray();
        }
    }
}
