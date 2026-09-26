using System;
using DarkNights.Runtime.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkNights.Entry.Terrain
{
    /// <summary>工作台场景笔刷与镜头交互；从场景开始的笔触才可绘制，跨过 GUI 中断，松键、失焦及换图立即结束事务。</summary>
    public sealed class TerrainWorkbenchPointer
    {
        private WorkshopTerrainEdits edits;
        private Vector2 lastMouse;
        private Vector2Int? lastCell;
        private bool painting, panning, cameraTool;
        public void Stop() { edits?.EndStroke(); painting = panning = false; lastCell = null; }
        public void Update(TerrainDebugPanel panel, bool blocked)
        {
            var boot = panel.Bootstrap; var flyer = boot.Flyer; var mouse = Mouse.current;
            if (edits != boot.Workshop?.Edits) { Stop(); edits = boot.Workshop?.Edits; }
            if (panel.MapTool == 0 && cameraTool) { flyer.ResetWorkbenchCamera(); cameraTool = false; }
            if (blocked || mouse == null || edits == null || panel.MapTool == 0 || !Application.isFocused)
            { Stop(); return; }
            cameraTool = true;
            var pos = mouse.position.ReadValue();
            if (pos.x < 0 || pos.y < 0 || pos.x >= Screen.width || pos.y >= Screen.height) { Stop(); return; }
            if (mouse.scroll.ReadValue().y != 0)
                flyer.CameraDistance = Mathf.Clamp(flyer.CameraDistance * (mouse.scroll.ReadValue().y > 0 ? .9f : 1.1f), 5, 600);
            if (mouse.middleButton.wasPressedThisFrame || (panel.MapTool == 1 && mouse.leftButton.wasPressedThisFrame))
            { panning = true; lastMouse = pos; }
            if (panning)
            {
                if (!mouse.middleButton.isPressed && !mouse.leftButton.isPressed) panning = false;
                else { flyer.PanWorkbenchCamera((lastMouse - pos) * (2 * flyer.CameraDistance / Screen.height)); lastMouse = pos; }
                return;
            }
            if (mouse.leftButton.wasPressedThisFrame && panel.MapTool >= 2) { painting = true; edits.BeginStroke(); }
            if (painting && !mouse.leftButton.isPressed) { Stop(); return; }
            if (!painting) return;
            var world = flyer.ViewCamera.ScreenToWorldPoint(new Vector3(pos.x, pos.y, 10));
            var cell = new Vector2Int(Mathf.FloorToInt(world.x + .5f), Mathf.FloorToInt(-world.y + .5f));
            panel.Run(() => PaintTo(panel, cell));
        }
        private void PaintTo(TerrainDebugPanel panel, Vector2Int cell)
        {
            var start = lastCell ?? cell;
            bool changed = edits.PaintLine(start.x, start.y, cell.x, cell.y, panel.MapTool == 3, panel.FillMaterial);
            lastCell = cell;
            if (changed) panel.Bootstrap.Preview.NotifyReplicaChanged();
        }
        public void DrawGrid(TerrainDebugPanel panel, float scale)
        {
            if (!panel.ShowGrid || Event.current.type != EventType.Repaint) return;
            var camera = panel.Bootstrap.Flyer.ViewCamera;
            if (camera == null) return;
            float pixelCell = Screen.height / (2 * camera.orthographicSize), left = panel.Visible ? panel.Layout.Panel.xMax + 4 : 0;
            int step = 1; while (pixelCell * step / scale < 14) step *= 2;
            var old = GUI.color; GUI.color = new Color(.8f, .95f, 1, .2f);
            for (int x = 0; x <= 320; x += step)
            {
                var screen = camera.WorldToScreenPoint(new Vector3(x - .5f, 0, 0)); float px = screen.x / scale;
                if (px >= left && px <= Screen.width / scale) GUI.DrawTexture(new Rect(px, 0, 1, Screen.height / scale), Texture2D.whiteTexture);
            }
            for (int y = 0; y <= 192; y += step)
            {
                var screen = camera.WorldToScreenPoint(new Vector3(0, -y + .5f, 0)); float py = (Screen.height - screen.y) / scale;
                if (py >= 0 && py <= Screen.height / scale) GUI.DrawTexture(new Rect(left, py, Screen.width / scale - left, 1), Texture2D.whiteTexture);
            }
            GUI.color = old;
        }
    }
}
