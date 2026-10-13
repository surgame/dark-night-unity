using System;
using AnyRules.Next;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>调参台相机的局部装载合同；视口外保留光束与规则邻域，区块对齐后才替换宿主，不扩展为完整地图。</summary>
    public static class FlashlightPreviewRegion
    {
        public static Vector3 CameraPosition(Vector2 actor, Vector2 offset)
            => new Vector3(actor.x - .5f + offset.x, -actor.y + 2.5f - offset.y, -10);

        public static GridBounds Capture(Vector3 camera, float size)
        {
            const int chunk = 32, halo = 32;
            int left = Math.Max(0, Mathf.FloorToInt((camera.x - size * 1.6f - halo) / chunk) * chunk);
            int right = Math.Min(320, Mathf.CeilToInt((camera.x + size * 1.6f + halo) / chunk) * chunk);
            int bottom = Math.Max(-192, Mathf.FloorToInt((camera.y - size - halo) / chunk) * chunk);
            int top = Math.Min(0, Mathf.CeilToInt((camera.y + size + halo) / chunk) * chunk);
            return new GridBounds(left, bottom, right - left, top - bottom);
        }

        public static bool ContainsView(GridBounds loaded, Vector3 camera, float size)
        {
            const int margin = 24;
            return loaded.IsValid && Math.Max(0, camera.x - size * 1.6f - margin) >= loaded.MinU &&
                Math.Min(320, camera.x + size * 1.6f + margin) <= loaded.MaxUExclusive &&
                Math.Max(-192, camera.y - size - margin) >= loaded.MinV &&
                Math.Min(0, camera.y + size + margin) <= loaded.MaxVExclusive;
        }
    }
}
