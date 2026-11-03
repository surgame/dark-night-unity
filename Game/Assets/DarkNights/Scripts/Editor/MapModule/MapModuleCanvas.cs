using System;
using System.Linq;
using DarkNights.Core.Logic.MapModule;
using DarkNights.Runtime.Physics;
using DarkNights.View.MapModule;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.MapModule
{
    /// <summary>
    /// 工作台的只读作者画布，直接绘制Scene实例轮廓，平移缩放和边界开关不更改作者数据。
    /// 点击只选择对应原生实例；主角仅作真实素材比例参考，不运行会话、运动或人物可达筛选。
    /// </summary>
    internal sealed class MapModuleCanvas
    {
        private Vector2 pan;
        private float zoom = 1;
        private bool fit = true;
        internal bool Bounds = true;
        internal bool Ports = true;
        internal Action<MapModuleAuthoring> Selected;
        internal Action<HangingRopePlacement> RopeSelected;
        internal void Fit() { fit = true; pan = Vector2.zero; zoom = 1; }
        internal void Draw(Rect rect, MapModuleCaseAuthoring sample, MapModuleAuthoring selected, HangingRopePlacement selectedRope = null)
        {
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(.14f, .22f, .21f) : new Color(.78f, .84f, .79f));
            if (sample == null || sample.Modules == null || sample.Modules.Length == 0)
            {
                GUI.Label(new Rect(rect.x + 20, rect.y + 20, rect.width - 40, 60), "请从左侧选择地图。");
                return;
            }
            var modules = sample.Modules.Where(m => m != null).ToArray();
            if (modules.Length == 0) return;
            float minX = modules.Min(m => m.transform.position.x) - .08f, minY = modules.Min(m => m.transform.position.y);
            float maxX = modules.Max(m => m.transform.position.x + m.Footprint.x * 3.2f) - .08f;
            float maxY = modules.Max(m => m.transform.position.y + m.Footprint.y * 3.2f);
            float scale = Mathf.Max(1, Mathf.Min((rect.width - 64) / (maxX - minX), (rect.height - 100) / (maxY - minY))) * zoom;
            Vector2 center = new Vector2((minX + maxX) / 2, (minY + maxY) / 2);
            Vector2 Project(Vector3 point) => rect.center + pan + new Vector2((point.x - center.x) * scale, -(point.y - center.y) * scale);
            if (fit) { pan = Vector2.zero; fit = false; }
            var ropes = modules.SelectMany(m => m.GetComponentsInChildren<HangingRopePlacement>(true)).Distinct().ToArray();
            GUI.BeginClip(rect);
            var hoveredRope = ropes.FirstOrDefault(r => HandleUtility.DistancePointLine(Event.current.mousePosition,
                Project(r.transform.position) - rect.position,
                Project(r.transform.position + Vector3.down * r.SegmentLength * r.SegmentCount) - rect.position) < 8);
            Handles.BeginGUI();
            foreach (var module in modules)
            {
                foreach (var solid in module.Solids ?? Array.Empty<MapModuleSolid>())
                {
                    if (solid?.Vertices == null || solid.Vertices.Length < 3) continue;
                    try
                    {
                        var points = solid.Vertices.Select(p => new Core.Config.MapModule.MapModuleVertex(p.x, p.y)).ToArray();
                        Handles.color = Color.white;
                        foreach (var triangle in MapModuleGeometry.Triangulate(points))
                            Handles.DrawSolidRectangleWithOutline(new[]
                            {
                                (Vector3)(Project(module.WorldPoint(new Vector2Int((int)triangle[0].X, (int)triangle[0].Y))) - rect.position),
                                (Vector3)(Project(module.WorldPoint(new Vector2Int((int)triangle[1].X, (int)triangle[1].Y))) - rect.position),
                                (Vector3)(Project(module.WorldPoint(new Vector2Int((int)triangle[2].X, (int)triangle[2].Y))) - rect.position),
                                (Vector3)(Project(module.WorldPoint(new Vector2Int((int)triangle[2].X, (int)triangle[2].Y))) - rect.position)
                            }, new Color(.52f, .57f, .44f), Color.clear);
                        Handles.color = new Color(.87f, .82f, .64f);
                        var line = solid.Vertices.Concat(new[] { solid.Vertices[0] })
                            .Select(p => (Vector3)(Project(module.WorldPoint(p)) - rect.position)).ToArray();
                        Handles.DrawAAPolyLine(2, line);
                    }
                    catch (Exception)
                    {
                        GUI.Label(new Rect(16, 16, rect.width - 32, 40), "轮廓有错误；检查会指出原因。");
                    }
                }
                if (Bounds)
                {
                    var p = Project(module.WorldPoint(Vector2Int.zero)) - rect.position;
                    var box = new Rect(p.x, p.y - module.Footprint.y * 3.2f * scale,
                        module.Footprint.x * 3.2f * scale, module.Footprint.y * 3.2f * scale);
                    Handles.DrawSolidRectangleWithOutline(box, Color.clear,
                        module == selected ? new Color(.95f, .8f, .35f) : new Color(.48f, .62f, .52f));
                    GUI.Label(new Rect(box.x + 5, box.y + 5, box.width - 10, 24), module.DisplayName);
                    if (hoveredRope == null && Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                        box.Contains(Event.current.mousePosition))
                    { Selected?.Invoke(module); Event.current.Use(); }
                }
                if (Ports)
                    foreach (var port in module.Ports)
                    {
                        Handles.color = new Color(.95f, .72f, .33f);
                        Handles.DrawAAPolyLine(3, (Vector3)(Project(module.WorldPoint(port.From)) - rect.position),
                            (Vector3)(Project(module.WorldPoint(port.To)) - rect.position));
                    }
            }
            foreach (var rope in ropes)
            {
                Vector3 top = Project(rope.transform.position) - rect.position;
                var points = Enumerable.Range(0, Mathf.Clamp(rope.SegmentCount, 1, 12) + 1)
                    .Select(i => (Vector3)(Project(rope.transform.position + Vector3.down * (i * rope.SegmentLength)) - rect.position)).ToArray();
                Handles.color = rope == selectedRope ? new Color(1, .8f, .35f) : new Color(.65f, .43f, .19f);
                Handles.DrawAAPolyLine(rope == selectedRope ? 4 : 3, points);
                Handles.DrawSolidDisc(top, Vector3.forward, 5);
                foreach (var point in points) Handles.DrawWireDisc(point, Vector3.forward, 3);
                GUI.Label(new Rect(top.x + 10, top.y - 10, 120, 24), "吊绳 " + rope.RopeId + " · " + rope.SegmentCount + "节");
            }
            if (hoveredRope != null && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            { RopeSelected?.Invoke(hoveredRope); Event.current.Use(); }
            Handles.EndGUI();
            Handles.color = Color.white;
            var hero = AssetDatabase.LoadAssetAtPath<Texture2D>(MapModulePaths.Hero);
            if (sample.Spawn != null && hero != null)
            {
                var foot = Project(sample.Spawn.position) - rect.position;
                float h = .48f * scale, w = h * hero.width / hero.height;
                GUI.DrawTexture(new Rect(foot.x - w / 2, foot.y - h, w, h), hero, ScaleMode.ScaleToFit, true);
            }
            if (Event.current.type == EventType.ScrollWheel)
            { zoom = Mathf.Clamp(zoom * Mathf.Exp(-Event.current.delta.y * .07f), .5f, 5); Event.current.Use(); }
            if (Event.current.type == EventType.MouseDrag && Event.current.button == 2)
            { pan += Event.current.delta; Event.current.Use(); }
            GUI.EndClip();
        }
    }
}
