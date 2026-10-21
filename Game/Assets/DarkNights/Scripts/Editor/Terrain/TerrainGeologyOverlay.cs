using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>完整地图画布中的塌方编辑叠层；使用同一图像变换和冻结轨迹标出源岩、路径、包络及退避，不启动会话或显示玩家定位标记。</summary>
    internal static class TerrainGeologyOverlay
    {
        private static string identity;
        private static GeologyEventConfig frozenConfig;
        internal static void Draw(Rect clip, Rect bounds, GeologyDesignPoint point)
        {
            if (point == null || !point.Enabled) return;
            GUI.BeginClip(clip);
            bounds.position -= clip.position;
            void Box(int u, int v, int width, int height, Color color)
            {
                var rect = new Rect(bounds.x + (u - .5f) / 320 * bounds.width,
                    bounds.y + (-v - height + .5f) / 192 * bounds.height,
                    width / 320f * bounds.width, height / 192f * bounds.height);
                EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, .18f));
                Handles.color = color; Handles.DrawAAPolyLine(2, new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMax, rect.yMin),
                    new Vector3(rect.xMax, rect.yMax), new Vector3(rect.xMin, rect.yMax), new Vector3(rect.xMin, rect.yMin));
            }
            try
            {
                var config = point.Configuration;
                // 标签放在常驻图例，避免全图缩放时多个小范围的文字堆叠。
                Box(point.AllowedU, point.AllowedV, point.AllowedWidth, point.AllowedHeight, Color.gray);
                Box(point.RetreatU, point.RetreatV, point.RetreatWidth, point.RetreatHeight, Color.green);
                Box(config.SourceU, config.SourceV, config.Width, config.SourceRows, Color.cyan);
                if (config.EnterRegion) Box(config.RegionU, config.RegionV, config.RegionWidth, config.RegionHeight, new Color(1, .6f, .2f));
                else Box(config.SupportU, config.SupportV, 1, 1, Color.yellow);
                string next = JsonUtility.ToJson(config);
                if (identity != next) { frozenConfig = config.Freeze(); identity = next; }
                var frozen = frozenConfig;
                for (int i = 0; i < frozen.RubbleCount; i++)
                { var cell = frozen.Landing(i); Box(cell.U, cell.V, 1, 1, new Color(.8f, .5f, .3f)); }
                for (int i = 0; i < frozen.Collapse.Count; i += 4)
                {
                    var path = new Vector3[(frozen.Collapse.Frames - 1) / 6 + 1];
                    for (int frame = 0; frame < path.Length; frame++)
                    {
                        frozen.Collapse.Sample(i, frame * 6 * DarkNights.Core.ViewData.RubbleCollapseViewData.FrameSeconds, out float x, out float y, out _);
                        path[frame] = new Vector3(bounds.x + x / PlayableTerrain.CellPixels / 320 * bounds.width,
                            bounds.y + (PlayableTerrain.OriginY - y) / PlayableTerrain.CellPixels / 192 * bounds.height);
                    }
                    Handles.color = new Color(.7f, .8f, 1, .6f); Handles.DrawAAPolyLine(1, path);
                }
            }
            catch (Exception) { }
            finally { GUI.EndClip(); }
        }
    }
}
