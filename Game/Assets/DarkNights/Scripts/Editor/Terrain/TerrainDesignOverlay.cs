using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Expedition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 同版白模的设计点范围、旗标与查格；从最终站点和当前表现草稿捕获静态注释，不创建事件或推进回放。
    /// 岩体、矿物、保护格由属性表面表达；所有注释沿同一画布坐标变换，关闭图层即撤去对应标记。
    /// </summary>
    internal sealed class TerrainDesignOverlay
    {
        private readonly MapWorkbenchWindow owner;
        private readonly List<TerrainDesignMarker> markers = new List<TerrainDesignMarker>();
        private Vector2? pressed;
        private Vector2Int? selected;
        private PlayableTerrain captured;
        internal TerrainDesignOverlay(MapWorkbenchWindow owner) { this.owner = owner; }
        internal string Legend => "岩体浅灰／洞腔深灰；紫斜纹＝保护，绿点＝软岩。\n" +
            "矿格：铁蓝方块、金黄菱形、铜橙十字、银灰横条、钻绿圆；斜纹＝墙后矿。\n" +
            "旗标：N巢／封壳，S源岩，T触发，P支撑，L落点，R退避，A允许改形，D装饰；泊位沿原标记。";

        internal void Capture()
        {
            markers.Clear();
            var map = owner.Preview.Displayed;
            if (map == null) return;
            if (!ReferenceEquals(captured, map)) selected = null;
            captured = map;
            foreach (var nest in map.Nests)
            {
                Add(nest.U, -nest.Row, 1, 2, new Color(1, .72f, .25f), "N" + nest.Id + " 封壳");
                Add(nest.U + 1, -nest.Row, 4, 3, new Color(.85f, .55f, .95f), "N" + nest.Id + " 巢腔→");
            }
            foreach (var site in map.GeologySites)
            {
                Add(site.AllowedU, site.AllowedV, site.AllowedWidth, site.AllowedHeight, Color.gray, "A " + site.SiteId);
                Add(site.RetreatU, site.RetreatV, site.RetreatWidth, site.RetreatHeight, new Color(.4f, .9f, .55f), "R 退避");
                var config = JsonUtility.FromJson<GeologyEventConfig>(site.Configuration).Freeze();
                Add(config.SourceU, config.SourceV, config.Width, config.SourceRows, Color.cyan, "S 源岩");
                if (config.EnterRegion)
                    Add(config.RegionU, config.RegionV, config.RegionWidth, config.RegionHeight, new Color(1, .55f, .25f), "T 进入触发");
                else Add(config.SupportU, config.SupportV, 1, 1, Color.yellow, "P 支撑触发");
                for (int i = 0; i < config.RubbleCount; i++)
                {
                    var cell = config.Landing(i);
                    Add(cell.U, cell.V, 1, 1, new Color(.82f, .55f, .32f), "L 瓦砾" + i);
                }
            }
            if (owner.Style == null) return;
            var style = owner.Styles.Draft(owner.Style);
            var layout = SurfaceSceneryLayout.Find(style.SurfaceEnvironment, map.Background);
            if (layout == null) return;
            foreach (var item in owner.Styles.Draft(layout).Placements)
            {
                if (item == null || !item.Enabled || SurfaceSceneryLayout.IsGroundTrim(item)) continue;
                int radius = item.SupportRadius;
                Add(item.Column - radius, -item.Row, radius * 2 + 1, 1,
                    new Color(.35f, .85f, .65f), "D " + item.Id, true);
            }
        }

        private void Add(int u, int v, int width, int height, Color color, string label, bool scenery = false)
        {
            markers.Add(new TerrainDesignMarker(new Rect(u - .5f, -v - height + .5f, width, height), color, label, scenery));
        }

        internal void Input(Rect clip, Rect bounds, bool current)
        {
            var input = Event.current;
            if (!current) { pressed = null; return; }
            if (input.type == EventType.MouseDown && input.button == 0 && clip.Contains(input.mousePosition))
                pressed = input.mousePosition;
            if (input.type == EventType.MouseDrag && pressed.HasValue &&
                Vector2.Distance(pressed.Value, input.mousePosition) > 4) pressed = null;
            if (input.type == EventType.MouseUp && input.button == 0)
            {
                if (pressed.HasValue && bounds.Contains(input.mousePosition) &&
                    Vector2.Distance(pressed.Value, input.mousePosition) <= 4)
                {
                    int x = Mathf.RoundToInt((input.mousePosition.x - bounds.x) / bounds.width * 320);
                    int row = Mathf.RoundToInt((input.mousePosition.y - bounds.y) / bounds.height * 192);
                    if (x >= 0 && x < 320 && row >= 0 && row < 192) Inspect(x, row);
                }
                pressed = null;
            }
            if (input.type == EventType.MouseLeaveWindow) pressed = null;
        }

        private void Inspect(int x, int row)
        {
            selected = new Vector2Int(x, row);
            var map = owner.Preview.Displayed;
            int index = row * 320 + x;
            var materials = owner.Preview.Map.CopyMaterials(); var shapes = owner.Preview.Map.CopyShapes();
            string[] names = { "空气", "壤土", "板岩", "玄武岩", "铜材", "铁材", "金材", "苔岩", "基岩" };
            string detail = "U " + x + " / V " + -row + " · " + names[materials[index]] +
                " · 坡形 " + (TerrainCellShape)shapes[index];
            if (map.CopyProtection()[index]) detail += " · 保护格";
            if (map.CopySoftRock()[index]) detail += " · 软岩";
            if (map.Minerals != null)
            {
                byte kind = map.Minerals.Kind(index);
                if (kind > 0) detail += " · " + FrozenMineralRules.Keys[kind - 1] + " 储量 " + map.Minerals.Reserves(index);
            }
            else
                foreach (var deposit in map.Deposits)
                    foreach (var cell in deposit.Cells)
                        if (cell.U == x && cell.V == -row)
                            detail += " · " + deposit.Id + " / " + deposit.MineralKind + " 储量 " + cell.Capacity;
            var hits = markers.Where(m => m.Cells.Contains(new Vector2(x, row))).Select(m => m.Label).ToArray();
            if (hits.Length > 0) detail += "\n" + string.Join(" · ", hits.Take(8)) + (hits.Length > 8 ? " …" : "");
            owner.Preview.Design.SelectionDetail = detail;
            owner.Refresh();
        }

        internal void Draw(Rect clip, Rect bounds)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.BeginClip(clip); bounds.position -= clip.position;
            float step = bounds.width / 320;
            var localClip = new Rect(Vector2.zero, clip.size);
            Color previous = Handles.color;
            foreach (var marker in markers)
            {
                if (marker.Scenery ? !owner.State.DesignScenery : !owner.State.Sites) continue;
                var cells = marker.Cells;
                var rect = new Rect(bounds.x + cells.x * step, bounds.y + cells.y * step, cells.width * step, cells.height * step);
                if (!localClip.Overlaps(rect)) continue;
                EditorGUI.DrawRect(rect, new Color(marker.Color.r, marker.Color.g, marker.Color.b, .15f));
                Handles.color = marker.Color;
                Handles.DrawAAPolyLine(1, new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMax, rect.yMin),
                    new Vector3(rect.xMax, rect.yMax), new Vector3(rect.xMin, rect.yMax), new Vector3(rect.xMin, rect.yMin));
                var point = new Vector2(rect.center.x, rect.yMin);
                EditorGUI.DrawRect(new Rect(point.x, point.y - 9, 1, 9), marker.Color);
                EditorGUI.DrawRect(new Rect(point.x, point.y - 9, 5, 4), marker.Color);
                if (step >= 5 || !marker.Scenery && marker.Label.StartsWith("N", StringComparison.Ordinal))
                    GUI.Label(new Rect(point.x + 7, point.y - 17, 155, 18), marker.Label, EditorStyles.whiteMiniLabel);
            }
            if (selected.HasValue)
            {
                var cell = selected.Value;
                var rect = new Rect(bounds.x + (cell.x - .5f) * step, bounds.y + (cell.y - .5f) * step, step, step);
                Handles.color = Color.white;
                Handles.DrawAAPolyLine(2, new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMax, rect.yMin),
                    new Vector3(rect.xMax, rect.yMax), new Vector3(rect.xMin, rect.yMax), new Vector3(rect.xMin, rect.yMin));
            }
            Handles.color = previous; GUI.EndClip();
        }
    }
}
