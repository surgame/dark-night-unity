using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.View.Terrain
{
    /// <summary>原生主角 HUD 下的单个白色网格几何；跟随相机后的逻辑格位置，不占用射线，也不持有或修改地图。</summary>
    public sealed class TerrainMiningSelectorGraphic : UiShapeGraphic
    {
        private TerrainMiningSelectorSettings settings;
        private Vector2 center;
        private float cellPixels, screenScale = 1;
        private bool visible, valid, fading;

        public void Present(Camera camera, Vector3 worldCenter, TerrainMiningSelectorSettings style, bool show, bool allowed)
        {
            raycastTarget = false;
            visible = show; valid = allowed; settings = style;
            if (!visible || camera == null || style == null) { SetVerticesDirty(); return; }
            Vector3 screen = camera.WorldToScreenPoint(worldCenter);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, null, out var local);
            Rect bounds = rectTransform.rect;
            center = new Vector2(local.x - bounds.xMin, bounds.yMax - local.y);
            Vector3 next = camera.WorldToScreenPoint(worldCenter + Vector3.right * (Core.Config.Terrain.PlayableTerrain.CellPixels / 100f));
            screenScale = canvas == null ? 1 : canvas.scaleFactor;
            cellPixels = Mathf.Abs(next.x - screen.x) / screenScale;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!visible || settings == null || cellPixels <= 0) return;
            int radius = Mathf.Clamp(settings.Radius, 1, 2);
            float extent = (radius + 1) * cellPixels;
            float width = Mathf.Clamp(settings.LinePixels, 1, 3) / screenScale;
            Color white = new Color(1, 1, 1, valid ? 1 : Mathf.Clamp01(settings.InvalidOpacity));
            fading = true;
            int segments = (radius + 1) * 16;
            for (int boundary = -radius; boundary <= radius + 1; boundary++)
            {
                float offset = (boundary - .5f) * cellPixels;
                for (int segment = 0; segment < segments; segment++)
                {
                    float start = -extent + extent * 2 * segment / segments;
                    float end = -extent + extent * 2 * (segment + 1) / segments;
                    Line(mesh, center + new Vector2(offset, start), center + new Vector2(offset, end), white, width);
                    Line(mesh, center + new Vector2(start, offset), center + new Vector2(end, offset), white, width);
                }
            }
            fading = false;
            var selected = new Rect(center.x - cellPixels * .5f, center.y - cellPixels * .5f, cellPixels, cellPixels);
            Box(mesh, selected, new Color(1, 1, 1, white.a * Mathf.Clamp01(settings.FillOpacity)));
            white.a *= Mathf.Clamp01(settings.CenterOpacity);
            if (valid) Border(mesh, selected, white, width);
            else
                for (int edge = 0; edge < 4; edge++)
                    for (int dash = 0; dash < 4; dash++)
                    {
                        float start = cellPixels * dash / 4, end = start + cellPixels / 8;
                        Vector2 origin = edge < 2 ? selected.min : selected.max;
                        Vector2 direction = edge % 2 == 0 ? Vector2.right : Vector2.up;
                        if (edge >= 2) direction = -direction;
                        Line(mesh, origin + direction * start, origin + direction * end, white, width);
                    }
        }

        protected override Color VertexTint(Vector2 point, Color tint)
        {
            if (fading)
            {
                float radius = (Mathf.Clamp(settings.Radius, 1, 2) + 1) * cellPixels;
                float falloff = Mathf.Clamp01(1 - Vector2.Distance(point, center) / radius);
                tint.a *= Mathf.Clamp01(settings.SurroundOpacity) * Mathf.Pow(falloff, Mathf.Clamp(settings.FadePower, .5f, 4));
            }
            return tint;
        }
    }
}
