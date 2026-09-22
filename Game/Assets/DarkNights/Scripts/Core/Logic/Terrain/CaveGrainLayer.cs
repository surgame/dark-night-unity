using System;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>一个 modifier 输出的局部岩粒着色权重；随不可变轮廓保存，材质烘焙按栈顺序混合，不包含动态光或玩法状态。</summary>
    public sealed class CaveGrainLayer
    {
        private readonly float[] weights;
        private readonly byte[] tones;
        public double StoneSize { get; }
        public CaveGrainLayer(float[] weights, byte[] tones, double stoneSize)
        {
            if (weights == null || tones == null || weights.Length != tones.Length ||
                double.IsNaN(stoneSize) || stoneSize < 2 || stoneSize > 12) throw new ArgumentException("岩粒字段无效。");
            this.weights = (float[])weights.Clone(); this.tones = (byte[])tones.Clone(); StoneSize = stoneSize;
        }
        public int Length => weights.Length;
        public float Weight(int index) => weights[index];
        public byte Tone(int index) => tones[index];
    }
}
