using System;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Lighting;

namespace DarkNights.Tools.Lighting
{
    /// <summary>不依赖 Unity 导入的灯口边界探针；执行真实 Core 实现，验证非零截面、圆弧距离、背光和冻结规则校验。</summary>
    internal static class ProfileProbe
    {
        private static int Main()
        {
            int checks = 0;
            void Check(bool result, string label) { if (!result) throw new Exception(label); checks++; }
            float Beam(float x, float y, float width = .375f, bool directional = true) =>
                LightBeamProfile.Evaluate(x, y, 14, 90, width, .045f, directional);
            Check(Beam(.02f, .15f) > .9f, "Near mouth has a finite cross-section");
            Check(Beam(.02f, .15f, 0) == 0, "Point emitter stays narrow near mouth");
            Check(Beam(-.01f, 0) == 0, "No direct illumination behind aperture");
            Check(Beam(14, 0) == 0, "Range ends at the existing radius");
            Check(Beam(13, 6, 2) == 0, "Wide mouth does not extend radial range");
            Check(Beam(2, 1) == Beam(2, -1), "Profile is symmetric");
            Check(Beam(-2, 0, .375f, false) > 0, "Radial devices retain all directions");
            Check(Beam(1, 0) > Beam(7, 0), "Distance attenuates energy");
            foreach (float width in new[] { -.01f, 2.01f, float.NaN, float.PositiveInfinity })
            {
                bool rejected = false;
                try { new LightEmissionRules(14, 90, 1, 2, 0, 1, 1, 1, width); }
                catch (ArgumentException) { rejected = true; }
                Check(rejected, "Invalid aperture width rejected");
            }
            Console.WriteLine("Lighting profile: " + checks + "/" + checks + " passed (Core only)");
            return 0;
        }
    }
}
