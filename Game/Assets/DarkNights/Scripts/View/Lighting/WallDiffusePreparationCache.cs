using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using DarkNights.View.Terrain;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>受光墙段准备的本地失效票据；仅缓存影响入射与分组的冻结输入，地形版本、边界安装、区域或有效灯变化必须重算，不持有业务状态。</summary>
    public sealed class WallDiffusePreparationCache
    {
        private readonly Vector4[] poses = new Vector4[16], beams = new Vector4[16];
        private readonly LightBounceRules[] returns = new LightBounceRules[16];
        private readonly bool[] active = new bool[16];
        private readonly float[] intensity = new float[16], feather = new float[16];
        private CaveVisualSource source;
        private LightBoundarySnapshot snapshot;
        private RectInt bounds;
        private long revision;
        private int count, benchmark;
        private bool ready;

        public bool Matches(CaveVisualSource current, LightBoundarySnapshot boundary, RectInt region,
            Vector4[] positions, Vector4[] directions, Vector4[] apertures, Vector4[] colors,
            Vector4[] shadows, LightBounceRules[] rules, int lights, int mode)
        {
            if (!ready || source != current || revision != current.GeometryRevision || snapshot != boundary ||
                bounds != region || count != lights || benchmark != mode) return false;
            for (int n = 0; n < lights; n++)
            {
                bool enabled = Enabled(positions[n], colors[n], rules[n]);
                if (active[n] != enabled) return false;
                if (!enabled) continue;
                if (!poses[n].Equals(positions[n]) || !beams[n].Equals(Beam(directions[n], apertures[n], shadows[n])) ||
                    returns[n].DiffuseNearDistance != rules[n].DiffuseNearDistance ||
                    returns[n].DiffuseFarDistance != rules[n].DiffuseFarDistance ||
                    returns[n].DiffuseMaxReach != rules[n].DiffuseMaxReach ||
                    returns[n].DiffuseMatchIncident != rules[n].DiffuseMatchIncident ||
                    returns[n].DiffuseCornerRadius != rules[n].DiffuseCornerRadius ||
                    intensity[n] != colors[n].w || feather[n] != shadows[n].y) return false;
            }
            return true;
        }

        public void Remember(CaveVisualSource current, LightBoundarySnapshot boundary, RectInt region,
            Vector4[] positions, Vector4[] directions, Vector4[] apertures, Vector4[] colors,
            Vector4[] shadows, LightBounceRules[] rules, int lights, int mode)
        {
            source = current; revision = current.GeometryRevision; snapshot = boundary;
            bounds = region; count = lights; benchmark = mode; ready = true;
            for (int n = 0; n < lights; n++)
            {
                active[n] = Enabled(positions[n], colors[n], rules[n]);
                poses[n] = positions[n]; beams[n] = Beam(directions[n], apertures[n], shadows[n]);
                returns[n] = rules[n]; intensity[n] = colors[n].w; feather[n] = shadows[n].y;
            }
        }

        public static bool Enabled(Vector4 position, Vector4 color, LightBounceRules rules) =>
            rules.Continuous && position.w > -.9f && color.w > 0;

        private static Vector4 Beam(Vector4 direction, Vector4 aperture, Vector4 shadow) =>
            new Vector4(direction.x, direction.y, aperture.x, shadow.x);
    }
}
