using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.View.Expedition
{
    /// <summary>
    /// 星图的有界像素星体、航线与本船标记绘制；颜色为蓝灰／琥珀，星点不使用业务RNG。
    /// 航线先裁切到视口，只有已登记的星体由调用者送入绘制，保持迷雾信息边界。
    /// </summary>
    internal static class StarMapArtwork
    {
        private static readonly Color Amber = new Color(.94f, .81f, .47f);
        public static void DrawStars(Painter2D painter, Rect bounds)
        {
            painter.fillColor = new Color(.43f, .51f, .69f);
            for (int i = 0; i < 90; i++)
            {
                float x = ((i * 173 + 47) % 997) / 997f * bounds.width;
                float y = ((i * 311 + 91) % 991) / 991f * bounds.height;
                Rectangle(painter, new Rect(x, y, i % 9 == 0 ? 2 : 1, i % 9 == 0 ? 2 : 1));
            }
        }
        public static void Planet(Painter2D painter, Vector2 point, int danger, bool selected)
        {
            var light = danger == 1 ? new Color(.66f, .74f, .76f) : danger == 2 ? new Color(.82f, .66f, .49f) : new Color(.65f, .66f, .77f);
            for (int y = -10; y <= 10; y++)
                for (int x = -10; x <= 10; x++)
                {
                    float distance = (x * x + y * y) / 100f;
                    if (distance > 1) continue;
                    int grain = ((x * 73) ^ (y * 151)) & 7;
                    float value = Mathf.Clamp01(-x * .028f - y * .025f + Mathf.Sqrt(1 - distance) * .6f + grain * .025f);
                    painter.fillColor = Color.Lerp(new Color(.1f, .13f, .21f), light, Mathf.Floor(value * 4) / 4);
                    Rectangle(painter, new Rect(point.x + x * 2, point.y + y * 2, 2, 2));
                }
            if (!selected) return;
            painter.strokeColor = Amber; painter.lineWidth = 2; painter.BeginPath();
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sy in new[] { -1f, 1f })
                {
                    Vector2 p = point + new Vector2(sx * 29, sy * 29);
                    painter.MoveTo(p + new Vector2(-sx * 9, 0)); painter.LineTo(p);
                    painter.LineTo(p + new Vector2(0, -sy * 9));
                }
            painter.Stroke();
        }
        private static void Rectangle(Painter2D painter, Rect r)
        {
            painter.BeginPath(); painter.MoveTo(new Vector2(r.xMin, r.yMin));
            painter.LineTo(new Vector2(r.xMax, r.yMin)); painter.LineTo(new Vector2(r.xMax, r.yMax));
            painter.LineTo(new Vector2(r.xMin, r.yMax)); painter.ClosePath(); painter.Fill();
        }
        public static void Route(Painter2D painter, Vector2 from, Vector2 to, bool preview, Rect bounds)
        {
            Vector2 delta = to - from; float start = 0, end = 1;
            if (!Clip(-delta.x, from.x - bounds.xMin, ref start, ref end) ||
                !Clip(delta.x, bounds.xMax - from.x, ref start, ref end) ||
                !Clip(-delta.y, from.y - bounds.yMin, ref start, ref end) ||
                !Clip(delta.y, bounds.yMax - from.y, ref start, ref end)) return;
            to = from + delta * end; from += delta * start;
            painter.strokeColor = preview ? new Color(.58f, .67f, .83f) : Amber;
            painter.lineWidth = preview ? 1 : 2; painter.BeginPath();
            float length = Vector2.Distance(from, to);
            for (float d = 0; d < length; d += preview ? 12 : 8)
            {
                painter.MoveTo(Vector2.Lerp(from, to, d / length));
                painter.LineTo(Vector2.Lerp(from, to, Mathf.Min(d + 5, length) / length));
            }
            painter.Stroke();
        }
        private static bool Clip(float direction, float distance, ref float start, ref float end)
        {
            if (Mathf.Abs(direction) < .00001f) return distance >= 0;
            float ratio = distance / direction;
            if (direction < 0) { if (ratio > end) return false; start = Mathf.Max(start, ratio); }
            else { if (ratio < start) return false; end = Mathf.Min(end, ratio); }
            return true;
        }
        public static void Ship(Painter2D painter, Vector2 p)
        {
            painter.fillColor = Amber; painter.BeginPath(); painter.MoveTo(p + new Vector2(0, -8));
            painter.LineTo(p + new Vector2(6, 6)); painter.LineTo(p + new Vector2(0, 3));
            painter.LineTo(p + new Vector2(-6, 6)); painter.ClosePath(); painter.Fill();
        }
    }
}
