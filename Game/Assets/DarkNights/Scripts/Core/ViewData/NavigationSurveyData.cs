using System;
using System.Linq;
using DarkNights.Core.Config.Expedition;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 共享星图勘测结果的不可变位图；覆盖既有正负4096坐标，每格16单位，显露半径28单位。
    /// 位图仅从权威实际航迹累加，冻结后可跨网络／存储使用，浏览和目标预览不改变记录。
    /// </summary>
    public sealed class NavigationSurveyData
    {
        public const int Limit = 4096, CellSize = 16, Width = 512, ByteCount = Width * Width / 8;
        public const float Radius = 28;
        public const int EncodedLength = (ByteCount + 2) / 3 * 4;
        private readonly byte[] bits;
        public string Encoded { get; }

        public NavigationSurveyData(string encoded)
        {
            if (encoded == null || encoded.Length != EncodedLength)
                throw new ArgumentException("星图勘测位图长度无效。", nameof(encoded));
            try { bits = Convert.FromBase64String(encoded); }
            catch (FormatException e) { throw new ArgumentException("星图勘测位图编码无效。", nameof(encoded), e); }
            if (bits.Length != ByteCount || Convert.ToBase64String(bits) != encoded)
                throw new ArgumentException("星图勘测位图不是规范编码。", nameof(encoded));
            Encoded = encoded;
        }

        private NavigationSurveyData(byte[] owned)
        {
            bits = owned;
            Encoded = Convert.ToBase64String(bits);
        }

        public bool IsRevealed(float x, float y)
        {
            if (!Finite(x) || !Finite(y) || Math.Abs(x) > Limit || Math.Abs(y) > Limit) return false;
            int index = Cell(y) * Width + Cell(x);
            return (bits[index >> 3] & (1 << (index & 7))) != 0;
        }

        public static NavigationSurveyData Initial(NavigationDefinition definition, float x, float y)
        {
            var first = definition.Nodes.First(n => !n.PreviewOnly);
            var empty = new NavigationSurveyData(new byte[ByteCount]);
            return empty.Reveal(first.X, first.Y, first.X, first.Y).Reveal(x, y, x, y);
        }

        public NavigationSurveyData Reveal(float ax, float ay, float bx, float by)
        {
            NavigationNodeDefinition.ValidateCoordinate(ax); NavigationNodeDefinition.ValidateCoordinate(ay);
            NavigationNodeDefinition.ValidateCoordinate(bx); NavigationNodeDefinition.ValidateCoordinate(by);
            byte[] changed = null;
            int minX = Cell(Math.Min(ax, bx) - Radius), maxX = Cell(Math.Max(ax, bx) + Radius);
            int minY = Cell(Math.Min(ay, by) - Radius), maxY = Cell(Math.Max(ay, by) + Radius);
            float dx = bx - ax, dy = by - ay, length = dx * dx + dy * dy;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x * CellSize - Limit + CellSize * .5f;
                    float py = y * CellSize - Limit + CellSize * .5f;
                    float t = length <= 0 ? 0 : Math.Clamp(((px - ax) * dx + (py - ay) * dy) / length, 0, 1);
                    float ex = px - ax - t * dx, ey = py - ay - t * dy;
                    if (ex * ex + ey * ey > Radius * Radius) continue;
                    int index = y * Width + x, mask = 1 << (index & 7);
                    if ((bits[index >> 3] & mask) != 0) continue;
                    if (changed == null) changed = (byte[])bits.Clone();
                    changed[index >> 3] |= (byte)mask;
                }
            return changed == null ? this : new NavigationSurveyData(changed);
        }

        private static int Cell(float v) => Math.Clamp((int)Math.Floor((v + Limit) / CellSize), 0, Width - 1);
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
