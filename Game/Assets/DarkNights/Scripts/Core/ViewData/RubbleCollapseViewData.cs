using System;

namespace DarkNights.Core.ViewData
{
    /// <summary>有界碎岩接触求解的冻结轨迹；作者参数生成后只读采样，不持有可写物理世界。数组在构造时复制，地图只消费最终轮廓，视图不能反向改变落点或阻路。</summary>
    public sealed class RubbleCollapseViewData
    {
        private readonly float[] samples;
        private readonly RubbleFragmentViewData[] fragments;
        private readonly double[] impacts;
        public const double FrameSeconds = 1d / 90;
        public int Count => fragments.Length;
        public int Frames { get; }
        public double Duration => (Frames - 1) * FrameSeconds;
        public float FloorY { get; }

        public RubbleCollapseViewData(RubbleFragmentViewData[] fragments, float[] samples, double[] impacts, int frames, float floorY)
        {
            if (fragments == null || samples == null || frames < 2 || samples.Length != frames * fragments.Length * 3)
                throw new ArgumentException("碎岩轨迹的冻结尺寸无效。");
            this.fragments = (RubbleFragmentViewData[])fragments.Clone();
            this.samples = (float[])samples.Clone(); this.impacts = (double[])impacts.Clone(); Frames = frames; FloorY = floorY;
        }

        public RubbleFragmentViewData Fragment(int index) => fragments[index];
        public double ImpactAt(int index) => impacts[index];
        public void Sample(int index, double clock, out float x, out float y, out float angle)
        {
            int frame = (int)Math.Max(0, Math.Min(Frames - 1, clock / FrameSeconds));
            int offset = (frame * Count + index) * 3;
            x = samples[offset]; y = samples[offset + 1]; angle = samples[offset + 2];
        }
    }
}
