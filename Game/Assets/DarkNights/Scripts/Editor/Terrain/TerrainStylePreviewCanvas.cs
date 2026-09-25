using System;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>全图预览画布的编辑态交互；网格和悬停仅作叠层，鼠标笔触转换为逻辑格坐标。</summary>
    public sealed class TerrainStylePreviewCanvas
    {
        public TerrainStylePreviewTool ActiveTool { get; set; }
        public bool ShowGrid { get; set; } = true;
        public float Zoom { get; private set; } = 1;
        private Vector2 offset;
        private Vector2Int? hover, previous;
        private bool panning, painting;

        public void Stop(Action endStroke = null)
        { if (painting) endStroke?.Invoke(); panning = false; painting = false; previous = null; }
        public void Fit() { Zoom = 1; offset = Vector2.zero; }

        public void SetZoom(float value, Vector2 pivot, bool anchored)
        {
            float old = Zoom;
            Zoom = Mathf.Clamp(value, .5f, 8);
            if (anchored && !Mathf.Approximately(old, Zoom))
                offset = pivot + (offset - pivot) * (Zoom / old);
        }

        public void Input(Rect canvas, Texture image, Func<int, int, bool> paint, Action changed,
            Action beginStroke = null, Action endStroke = null, Func<bool> undo = null, Func<bool> redo = null)
        {
            var input = Event.current;
            if (input.type == EventType.KeyDown && (input.control || input.command) &&
                ((input.keyCode == KeyCode.Z && (input.shift ? redo : undo)?.Invoke() == true) ||
                 (input.keyCode == KeyCode.Y && redo?.Invoke() == true)))
            { changed(); input.Use(); return; }
            bool inside = canvas.Contains(input.mousePosition);
            if (input.type == EventType.ScrollWheel && inside)
            {
                SetZoom(Zoom * Mathf.Pow(1.1f, -input.delta.y), input.mousePosition - canvas.center, true);
                input.Use(); return;
            }
            if (input.type == EventType.MouseDown && inside &&
                (input.button == 2 || (input.button == 0 && ActiveTool == TerrainStylePreviewTool.Pan)))
            { panning = true; hover = null; input.Use(); return; }
            if (input.type == EventType.MouseDrag && panning)
            { offset += input.delta; input.Use(); return; }
            if (input.type == EventType.MouseUp && panning)
            { panning = false; input.Use(); return; }

            if (input.type == EventType.MouseMove || input.type == EventType.MouseDrag || input.type == EventType.MouseDown)
                hover = inside && image != null ? CellAt(input.mousePosition, canvas, image) : null;
            if (input.type == EventType.MouseDown && input.button == 0 && inside && ActiveTool != TerrainStylePreviewTool.Pan && hover.HasValue)
            { beginStroke?.Invoke(); painting = true; previous = null; PaintTo(hover.Value, paint, changed); input.Use(); }
            else if (input.type == EventType.MouseDrag && input.button == 0 && painting)
            {
                if (hover.HasValue) PaintTo(hover.Value, paint, changed);
                else previous = null;
                input.Use();
            }
            else if (input.type == EventType.MouseUp && input.button == 0 && painting)
            { endStroke?.Invoke(); painting = false; previous = null; input.Use(); }
            if (input.type == EventType.MouseLeaveWindow) { hover = null; Stop(endStroke); }
        }

        private void PaintTo(Vector2Int target, Func<int, int, bool> paint, Action changed)
        {
            var start = previous ?? target;
            int x = start.x, y = start.y;
            int dx = Math.Abs(target.x - x), dy = Math.Abs(target.y - y);
            int sx = x < target.x ? 1 : -1, sy = y < target.y ? 1 : -1;
            int error = dx - dy;
            bool edited = false;
            while (true)
            {
                edited |= paint(x, y);
                if (x == target.x && y == target.y) break;
                int twice = error * 2;
                if (twice > -dy) { error -= dy; x += sx; }
                if (twice < dx) { error += dx; y += sy; }
            }
            previous = target;
            if (edited) changed();
        }

        private Rect ImageBounds(Rect canvas, Texture image)
        {
            float scale = Mathf.Min(canvas.width / image.width, canvas.height / image.height) * Zoom;
            var center = new Vector2(canvas.width * .5f, canvas.height * .5f) + offset;
            return new Rect(center.x - image.width * scale * .5f,
                center.y - image.height * scale * .5f, image.width * scale, image.height * scale);
        }

        private Vector2Int? CellAt(Vector2 mouse, Rect canvas, Texture image)
        {
            Rect bounds = ImageBounds(canvas, image);
            float x = mouse.x - canvas.x - bounds.x, y = mouse.y - canvas.y - bounds.y;
            if (x < 0 || y < 0 || x >= bounds.width || y >= bounds.height) return null;
            return new Vector2Int(Mathf.FloorToInt(x / bounds.width * image.width / 8),
                Mathf.FloorToInt(y / bounds.height * image.height / 8));
        }

        public void Draw(Rect canvas, Texture image, int fps, int renderMilliseconds, bool pending, string pendingReason = null)
        {
            EditorGUI.DrawRect(canvas, new Color(.12f, .13f, .15f));
            GUI.BeginGroup(canvas);
            if (image != null)
            {
                Rect bounds = ImageBounds(canvas, image);
                GUI.DrawTexture(bounds, image, ScaleMode.StretchToFill, false);
                DrawOverlay(bounds, image);
            }
            else GUI.Label(new Rect(16, 38, canvas.width - 32, 40), "正在生成完整画面…", EditorStyles.whiteLabel);
            string action = ActiveTool == TerrainStylePreviewTool.Pan ? "左键拖拽平移" :
                ActiveTool == TerrainStylePreviewTool.Dig ? "左键单击或拖动拆格" : "左键单击或拖动填格";
            GUI.Box(new Rect(8, 8, Mathf.Min(canvas.width - 16, 335), 42),
                "预览画布 " + fps + " FPS · 最近相机渲染 " + renderMilliseconds + " ms\n" +
                action + " · 中键平移 · 滚轮缩放");
            if (pending) GUI.Label(new Rect(8, canvas.height - 30, canvas.width - 16, 22),
                pendingReason ?? "画面更新中…", EditorStyles.whiteLabel);
            GUI.EndGroup();
        }

        private void DrawOverlay(Rect bounds, Texture image)
        {
            float step = bounds.width * 8 / image.width;
            if (ShowGrid)
            {
                var tint = new Color(.87f, .73f, .40f, .20f);
                int stride = Mathf.Max(1, Mathf.CeilToInt(3f / step));
                for (int x = 0; x <= image.width / 8; x += stride)
                    EditorGUI.DrawRect(new Rect(bounds.x + x * step, bounds.y, 1, bounds.height), tint);
                for (int y = 0; y <= image.height / 8; y += stride)
                    EditorGUI.DrawRect(new Rect(bounds.x, bounds.y + y * step, bounds.width, 1), tint);
            }
            if (!hover.HasValue || ActiveTool == TerrainStylePreviewTool.Pan) return;
            var cell = hover.Value;
            var marker = new Rect(bounds.x + cell.x * step, bounds.y + cell.y * step, step, step);
            Color color = ActiveTool == TerrainStylePreviewTool.Fill ? new Color(.60f, .86f, .60f, .32f) :
                new Color(.95f, .70f, .44f, .32f);
            EditorGUI.DrawRect(marker, color);
        }

    }
}
