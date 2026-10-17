using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Experiments.RichVeinNests
{
    /// <summary>
    /// 从冻结矿格提取露头向内的主轴及中段接触点，仅用于侧旁巢腔的离线几何筛选。
    /// 揭露序来自R1，不代表实际采掘路线；接触点后保留至少四成矿格，避免末梢才遇巢。
    /// </summary>
    public sealed class VeinSideProfile
    {
        private readonly VeinInput vein;
        public int Dx { get; }
        public int Dy { get; }
        public int Width => Dx == 0 ? 3 : 5;
        public int Height => Dx == 0 ? 5 : 3;
        public double Extent { get; }

        public VeinSideProfile(VeinInput vein)
        {
            this.vein = vein;
            int far = vein.Cells.OrderByDescending(c => Distance(c, vein.Head)).First();
            int x = far % 320 - vein.Head % 320, y = far / 320 - vein.Head / 320;
            Dx = Math.Abs(x) >= Math.Abs(y) ? Math.Sign(x) : 0;
            Dy = Dx == 0 ? Math.Sign(y) : 0;
            Extent = vein.Cells.Max(Along);
        }

        public double Along(int cell) => (cell % 320 - vein.Head % 320) * Dx +
            (cell / 320 - vein.Head / 320) * Dy;

        public IEnumerable<int> ContactIndices()
        {
            return Enumerable.Range(1, vein.Cells.Count - 1)
                .Where(i => i + 1 >= Math.Ceiling(vein.Cells.Count * .25) &&
                    i + 1 <= Math.Floor(vein.Cells.Count * .60) && Extent >= 4 &&
                    Along(vein.Cells[i]) / Extent >= .25 && Along(vein.Cells[i]) / Extent <= .65)
                .OrderBy(i => Math.Abs((i + 1.0) / vein.Cells.Count - .4));
        }

        public bool Beside(int contact, int center)
        {
            int x = center % 320 - contact % 320, y = center / 320 - contact / 320;
            return Math.Abs(x * Dx + y * Dy) <= 1 && Math.Abs(x * -Dy + y * Dx) >= 3 &&
                Along(center) / Extent >= .2 && Along(center) / Extent <= .7;
        }

        private static int Distance(int a, int b)
        {
            int x = a % 320 - b % 320, y = a / 320 - b / 320;
            return x * x + y * y;
        }
    }
}
