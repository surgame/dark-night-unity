using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>一次烘焙持有的不可变轮廓参数；种子独立于地图和材质，单位为原生像素，不拥有地形状态。</summary>
    public sealed class CaveOutlineSettings
    {
        public CaveOutlineMode Mode { get; }
        public string Seed { get; }
        public double Amplitude { get; }
        public int Wavelength { get; }
        public int Quantization { get; }
        public bool Enabled => Mode != CaveOutlineMode.None && Amplitude > 0;
        public int Reach => Enabled ? (int)Math.Ceiling(1.5 * Amplitude + 2 * Quantization) + 2 : 0;
        public static CaveOutlineSettings Current => new CaveOutlineSettings(CaveOutlineMode.HybridB, "OUTLINE-0921", 3, 20, 2);
        public CaveOutlineSettings(CaveOutlineMode mode, string seed, double amplitude, int wavelength, int quantization)
        {
            if (!Enum.IsDefined(typeof(CaveOutlineMode), mode) || string.IsNullOrWhiteSpace(seed) || seed.Length > 80 ||
                double.IsNaN(amplitude) || amplitude < 0 || amplitude > 8 || wavelength < 8 || wavelength > 96 || quantization < 1 || quantization > 4)
                throw new ArgumentException("外轮廓参数超出 H5 合同。");
            Mode = mode; Seed = seed; Amplitude = amplitude; Wavelength = wavelength; Quantization = quantization;
        }
        public string ModeKey => Mode == CaveOutlineMode.None ? "none" : Mode == CaveOutlineMode.Wave ? "wave" :
            Mode == CaveOutlineMode.PixelB ? "pixelB" : Mode == CaveOutlineMode.PixelC ? "pixelC" :
            Mode == CaveOutlineMode.HybridB ? "hybridB" : "hybridC";
    }
}
