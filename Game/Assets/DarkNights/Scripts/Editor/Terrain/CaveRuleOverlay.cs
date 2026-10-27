using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>在原Tuner最终地图上叠加制作区域、声明连接、矿层与侧巢；仅展示同候选冻结数据，不把连线作为人物路径证明。</summary>
    internal static class CaveRuleOverlay
    {
        private static bool regions = true, connections = true, minerals = true, nests = true;
        internal static void Configure(bool showRegions, bool showConnections, bool showMinerals, bool showNests)
        {
            regions = showRegions; connections = showConnections; minerals = showMinerals; nests = showNests;
        }
        internal static void DrawControls()
        {
            EditorGUILayout.BeginHorizontal();
            regions = GUILayout.Toggle(regions, "区域", "Button"); connections = GUILayout.Toggle(connections, "声明连接", "Button");
            minerals = GUILayout.Toggle(minerals, "墙后矿", "Button"); nests = GUILayout.Toggle(nests, "侧巢", "Button");
            EditorGUILayout.EndHorizontal();
        }
        internal static void Draw(Rect clip, Rect bounds, TerrainGenerationPreview model)
        {
            if (!model.HasRoomPlan || model.RulePlan == null || model.LastCandidate == null || Event.current.type != EventType.Repaint) return;
            var plan = model.RulePlan; var map = model.LastCandidate;
            GUI.BeginGroup(clip); bounds.position -= clip.position;
            Vector2 Point(float x, float y) => new Vector2(bounds.x + x / 320 * bounds.width, bounds.y + y / 192 * bounds.height);
            Handles.BeginGUI(); Color previous = Handles.color;
            if (connections)
            {
                Handles.color = new Color(.4f, .95f, 1, .75f);
                foreach (var edge in plan.Connections)
                {
                    var a = plan.Rooms[edge.From]; var b = plan.Rooms[edge.To];
                    Handles.DrawAAPolyLine(2, Point(a.X, a.Y), Point(edge.BendX, edge.BendY), Point(b.X, b.Y));
                    if (edge.CoverLength > 0) GUI.Label(new Rect(Point(edge.BendX, edge.BendY), new Vector2(70, 18)), "封岩支线");
                }
            }
            Handles.color = previous; Handles.EndGUI();
            if (regions)
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    var room = plan.Rooms[i]; Vector2 top = Point(room.Left, room.Top), bottom = Point(room.Left + room.Width, room.Top + room.Height);
                    var rect = Rect.MinMaxRect(top.x, top.y, bottom.x, bottom.y);
                    EditorGUI.DrawRect(rect, new Color(.25f, .65f, .9f, .08f));
                    int amount = map.Deposits.Where(d => d.Id.StartsWith("rule-r" + i + "-")).Sum(d => d.Capacity);
                    GUI.Label(new Rect(top, new Vector2(170, 36)), "R" + i + " · " + room.Kind + "\n路程" + (int)plan.Distances[i] + " · 储量" + amount, EditorStyles.whiteMiniLabel);
                }
            if (minerals)
                foreach (var deposit in map.Deposits) foreach (var cell in deposit.Cells)
                {
                    var point = Point(cell.U, -cell.V);
                    bool hidden = map.Material(cell.U, -cell.V) != 0;
                    EditorGUI.DrawRect(new Rect(point, new Vector2(bounds.width / 320, bounds.height / 192)),
                        hidden ? new Color(1, .7f, .15f, .75f) : new Color(.3f, 1, .55f, .75f));
                }
            if (nests)
                foreach (var site in map.Nests)
                    GUI.Label(new Rect(Point(site.U, site.Row - 3), new Vector2(75, 18)), "侧巢" + site.Id, EditorStyles.whiteMiniLabel);
            GUI.EndGroup();
        }
    }
}
