using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>巢穴运行状态的冻结保存副本；0休眠、1前兆、2出虫、3清理，阶段单向推进。客户端或文件不拥有推进权。</summary>
    public sealed class TerrainNestProgress
    {
        public int Id { get; }
        public int Phase { get; }
        public double Clock { get; }
        public float X { get; }
        public float Height { get; }
        public float VerticalSpeed { get; }
        public int Hp { get; }
        public TerrainNestProgress(int id, int phase, double clock, float x, float height, float verticalSpeed, int hp)
        {
            if (id < 1 || id > TerrainNest.MaximumCount || phase < 0 || phase > 3 ||
                double.IsNaN(clock) || double.IsInfinity(clock) || clock < 0 || clock > 10000000 ||
                float.IsNaN(x) || x < 0 || x > 5120 || float.IsNaN(height) || height < PlayableTerrain.MinimumHeight - 32 || height > 1024 ||
                float.IsNaN(verticalSpeed) || Math.Abs(verticalSpeed) > 1024 || hp < 0 || hp > 10000 || (phase == 3) != (hp == 0))
                throw new ArgumentException("巢穴保存阶段、姿态或耐久无效。");
            Id = id; Phase = phase; Clock = clock; X = x; Height = height; VerticalSpeed = verticalSpeed; Hp = hp;
        }
    }
}
