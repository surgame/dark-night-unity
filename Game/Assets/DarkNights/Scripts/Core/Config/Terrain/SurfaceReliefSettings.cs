using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>
    /// 地表带的可复现制作输入；高度以世界地表基准向上为正、单位为格。
    /// 默认关闭以保留旧图口径，进入生成前深复制；不持有运行地形或人物通行条件。
    /// </summary>
    [Serializable]
    public sealed class SurfaceReliefSettings
    {
        public const int Version = 1;
        public bool Enabled;
        public double BaseHeightCells;
        public double HeightCells = 8;
        public double WavelengthCells = 64;
        /// <summary>仅读取旧地图来源的历史参数；当前地表不消费泊位过渡，不生成平台。</summary>
        public int DockTransitionCells = 16;

        public SurfaceReliefSettings CopyValidated()
        {
            if (!Finite(BaseHeightCells) || Math.Abs(BaseHeightCells) > 8 ||
                !Finite(HeightCells) || HeightCells < 1 || HeightCells > 12 ||
                !Finite(WavelengthCells) || WavelengthCells < 24 || WavelengthCells > 128)
                throw new ArgumentException("地表带高度或尺度参数越界。");
            return (SurfaceReliefSettings)MemberwiseClone();
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
