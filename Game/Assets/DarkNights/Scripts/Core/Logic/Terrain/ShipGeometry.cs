using System;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>176×104 原生像素工作船的可行走合同；每像素两权威单位，坐标以两脚接地中点为原点。</summary>
    public static class ShipGeometry
    {
        // RampToe 是登船／下船交界处运动适配的共同边界；改变坡道美术或登船方式时同步检查 ShipCabinMotion。
        public const float RampToe = -160, RampHinge = -80, CabinRight = 128;
        public const float PilotX = 96, PilotHeight = 80, HatchX = -40, HatchHeight = 156;
        public const float HoldX = -40, HoldHeight = 40;
        public const float HalfWidth = 176, Roof = 168;

        public static float Floor(float x)
        {
            if (x < RampToe || x > CabinRight) throw new ArgumentOutOfRangeException(nameof(x));
            if (x < RampHinge) return (x - RampToe) * .5f;
            if (x <= 16) return 40;
            if (x < 80) return 40 + (x - 16) * .625f;
            return 80;
        }

        /// <summary>舱内顶棚高度；坡道段保持开放，舱体段按外壳轮廓限制乘员头顶。</summary>
        public static float CabinCeiling(float x)
        {
            if (x < RampHinge) return float.PositiveInfinity;
            if (x <= 40) return 134;
            if (x < 80) return 134 + (x - 40) * .85f;
            return Roof;
        }

        public static bool AtPilot(float x, float height) => Math.Abs(x - PilotX) <= 16 && Math.Abs(height - PilotHeight) <= 4;
        public static bool Inside(float x, float height) => x >= RampHinge + 8 && x <= CabinRight && Math.Abs(height - Floor(x)) <= 4;

        // 保守包络避让真实坡形；空出的腹部和起落架间隙不当成实心矩形。
        public static bool Hull(float x, float height) =>
            Math.Abs(x) <= HalfWidth && height >= 48 && height <= 120 ||
            x >= -104 && x <= 40 && height >= 34 && height <= 134 ||
            x >= 32 && x <= 140 && height >= 76 && height <= Roof ||
            (Math.Abs(x + 56) <= 18 || Math.Abs(x - 104) <= 18) && height >= 1 && height <= 48;
    }
}
