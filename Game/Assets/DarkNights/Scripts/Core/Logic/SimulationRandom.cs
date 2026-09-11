using System;

namespace DarkNights.Core.Logic
{
    /// <summary>
    /// 权威会话独占的 PCG32 随机状态，兼容原 Godot 的播种、闭区间整数和单精度范围采样。
    /// Seed 重播种，State 仅恢复位状态；显示逻辑不得消耗此序列。算法来源和验证见 docs/CORE_MIGRATION.md。
    /// </summary>
    public sealed class SimulationRandom
    {
        public const string Algorithm = "godot-pcg32-clz-f32-v1";
        private const ulong Increment = unchecked((1442695040888963407UL << 1) | 1UL);
        private ulong seed;
        public ulong State { get; set; }

        public ulong Seed
        {
            get => seed;
            set
            {
                seed = value;
                State = 0;
                NextUInt();
                State = unchecked(State + value);
                NextUInt();
            }
        }

        public uint NextUInt()
        {
            ulong previous = State;
            State = unchecked(previous * 6364136223846793005UL + Increment);
            uint shifted = (uint)(((previous >> 18) ^ previous) >> 27);
            int rotation = (int)(previous >> 59);
            return (shifted >> rotation) | (shifted << ((-rotation) & 31));
        }

        public int RandiRange(int from, int to)
        {
            if (from == to) return from;
            long minimum = Math.Min(from, to);
            uint difference = (uint)((long)Math.Max(from, to) - minimum);
            if (difference == uint.MaxValue) return (int)(minimum + NextUInt());
            uint bound = difference + 1;
            uint threshold = unchecked(0U - bound) % bound;
            uint value;
            do { value = NextUInt(); } while (value < threshold);
            return (int)(minimum + value % bound);
        }

        public float RandfRange(float from, float to)
        {
            uint exponent = NextUInt();
            float unit = 0;
            if (exponent != 0)
            {
                int zeros = 0;
                while ((exponent & 0x80000000U) == 0)
                {
                    zeros++;
                    exponent <<= 1;
                }
                double significand = RoundToSingle(NextUInt() | 0x80000001U);
                unit = RoundToSingle(significand * Math.Pow(2, -32 - zeros));
            }
            // Mono 可保留更宽的浮点中间值；显式位往返固定原生 float 每一步的舍入，避免跨运行时一位差异。
            double width = RoundToSingle((double)to - from);
            double scaled = RoundToSingle((double)unit * width);
            return RoundToSingle(scaled + from);
        }

        private static float RoundToSingle(double value) =>
            BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits((float)value));
    }
}
