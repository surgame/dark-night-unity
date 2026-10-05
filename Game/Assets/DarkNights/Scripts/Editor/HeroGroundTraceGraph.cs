using System;
using System.Collections.Generic;
using DarkNights.Runtime.Terrain;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>UI Toolkit的五秒运动时间线；有界绘制脚底间隙、竖直速度和支撑，并标出起跳及非起跳支撑丢失。</summary>
    internal sealed class HeroGroundTraceGraph : VisualElement
    {
        private IReadOnlyList<TerrainMotionTraceFrame> frames;
        private int selected, first, last;
        private int displayedCount = -1;
        internal Action<int> Selected;
        internal HeroGroundTraceGraph()
        {
            AddToClassList("trace-graph");
            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (frames == null || frames.Count == 0) return;
                float ratio = Mathf.Clamp01((e.localPosition.x - 8) / Mathf.Max(1, contentRect.width - 16));
                Selected?.Invoke(first + Mathf.RoundToInt(ratio * (last - first)));
            });
        }

        internal void Present(IReadOnlyList<TerrainMotionTraceFrame> source, int selection)
        {
            if (ReferenceEquals(frames, source) && displayedCount == source.Count && selected == selection) return;
            frames = source; displayedCount = source.Count; selected = selection; MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            float width = contentRect.width - 16, height = contentRect.height - 16;
            if (width < 1 || height < 1) return;
            var p = context.painter2D;
            p.lineWidth = 1; p.strokeColor = new Color(.22f, .28f, .35f);
            for (int i = 0; i <= 6; i++) Line(p, 8, 8 + height * i / 6, width + 8, 8 + height * i / 6);
            if (frames == null || frames.Count == 0) return;
            last = frames.Count - 1; first = Math.Max(0, selected - 180);
            last = Math.Min(last, first + 300); first = Math.Max(0, last - 300);
            float X(int index) => 8 + (index - first) * width / Math.Max(1, last - first);
            float gapScale = 1, speedScale = 1;
            for (int i = first; i <= last; i++)
            { gapScale = Math.Max(gapScale, frames[i].GapAfter ?? 0); speedScale = Math.Max(speedScale, Math.Abs(frames[i].SpeedAfter)); }
            p.lineWidth = 1.5f;
            for (int i = first; i <= last; i++)
            {
                var f = frames[i];
                p.lineWidth = 5;
                p.strokeColor = f.SupportAfter >= 0 ? new Color(.4f, .85f, .65f) : new Color(1, .64f, .3f);
                Line(p, X(i), 8 + height * .91f, X(Math.Min(i + 1, last)), 8 + height * .91f);
                p.lineWidth = 1.5f;
                if (f.JumpStarted || f.SupportBefore >= 0 && f.SupportAfter < 0 && !f.JumpStarted)
                {
                    p.strokeColor = f.JumpStarted ? new Color(.7f, .6f, 1) : new Color(1, .4f, .4f);
                    Line(p, X(i), 8, X(i), height + 8);
                }
                if (i == first) continue;
                var previous = frames[i - 1];
                p.strokeColor = new Color(.35f, .82f, .92f);
                if (previous.GapAfter.HasValue && f.GapAfter.HasValue)
                    Line(p, X(i - 1), 8 + height * .31f * (1 - Mathf.Clamp01(previous.GapAfter.Value / gapScale)),
                        X(i), 8 + height * .31f * (1 - Mathf.Clamp01(f.GapAfter.Value / gapScale)));
                p.strokeColor = new Color(.96f, .83f, .4f);
                Line(p, X(i - 1), 8 + height * (.59f - .19f * previous.SpeedAfter / speedScale),
                    X(i), 8 + height * (.59f - .19f * f.SpeedAfter / speedScale));
            }
            p.strokeColor = Color.white; p.lineWidth = 2;
            Line(p, X(selected), 8, X(selected), height + 8);
        }

        private static void Line(Painter2D p, float x0, float y0, float x1, float y1)
        { p.BeginPath(); p.MoveTo(new Vector2(x0, y0)); p.LineTo(new Vector2(x1, y1)); p.Stroke(); }
    }
}
