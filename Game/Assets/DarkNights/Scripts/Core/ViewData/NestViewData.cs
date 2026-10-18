using System;

namespace DarkNights.Core.ViewData
{
    /// <summary>巢穴与幼虫只读展示副本，坐标与矿层同属当前地图身份；不作为命中、伤害或恢复数据。</summary>
    public sealed class NestViewData
    {
        public int Id { get; }
        public int U { get; }
        public int Row { get; }
        public int Phase { get; }
        public double Clock { get; }
        public float X { get; }
        public float Height { get; }
        public int Hp { get; }
        public NestViewData(int id, int u, int row, int phase, double clock, float x, float height, int hp)
        {
            if (id < 1 || id > 32 || u < 0 || u >= 320 || row < 0 || row >= 192 || phase < 0 || phase > 3 ||
                double.IsNaN(clock) || double.IsInfinity(clock) || clock < 0 || clock > 10000000 ||
                float.IsNaN(x) || x < 0 || x > 5120 || float.IsNaN(height) || height < -3000 || height > 1024 || hp < 0 || hp > 10000 ||
                (phase == 3) != (hp == 0)) throw new ArgumentException("巢穴展示数据无效。");
            Id = id; U = u; Row = row; Phase = phase; Clock = clock; X = x; Height = height; Hp = hp;
        }
    }
}
