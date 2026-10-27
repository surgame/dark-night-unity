using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地图画布的站点选择与单点塌方取格；坐标沿原图像变换转成U／V，不增加运行对象或自动布点。
    /// 取格只编辑既有作者草稿，完成或退出任务后释放工具；最终有效性由完整候选静态校验决定。
    /// </summary>
    internal sealed class MapWorkbenchPicking
    {
        private readonly MapWorkbenchWindow owner;
        internal MapWorkbenchPicking(MapWorkbenchWindow owner) { this.owner = owner; }
        private static Vector2 Point(Rect bounds, int u, int row) => new Vector2(
            bounds.x + u / 320f * bounds.width, bounds.y + row / 192f * bounds.height);

        internal bool Handle(Rect clip, Rect bounds)
        {
            var input = Event.current;
            if (input.type == EventType.KeyDown && input.keyCode == KeyCode.Escape && owner.State.Pick != 0)
            { owner.State.Pick = 0; input.Use(); owner.Refresh(); return true; }
            if (input.type != EventType.MouseDown || input.button != 0 || !clip.Contains(input.mousePosition) ||
                !bounds.Contains(input.mousePosition)) return false;
            if (owner.State.Pick != 0)
            {
                if (!owner.Preview.IsCandidate || owner.Preview.Map.HasChanges)
                { owner.Notice = "请先查看草稿候选并取消临时拆填。"; input.Use(); return true; }
                int u = Mathf.Clamp(Mathf.FloorToInt((input.mousePosition.x - bounds.x) / bounds.width * 320 + .5f), 0, 319);
                int row = Mathf.Clamp(Mathf.FloorToInt((input.mousePosition.y - bounds.y) / bounds.height * 192 + .5f), 0, 191);
                int selection = owner.State.Pick;
                owner.Execute(() => owner.Commands.Edit("设置塌方设计坐标", () =>
                {
                    var p = owner.Draft.Config.Geology; var c = p.Configuration; int v = -row;
                    if (selection == 1) { c.SourceU = u; c.SourceV = v; }
                    if (selection == 2) { c.LandingU = u; c.LandingV = v; }
                    if (selection == 3) { c.SupportU = u; c.SupportV = v; }
                    if (selection == 4) { c.RegionU = u; c.RegionV = v; }
                    if (selection == 5) { p.RetreatU = u; p.RetreatV = v; }
                    if (selection == 6) { p.AllowedU = u; p.AllowedV = v; }
                    owner.State.Pick = 0;
                }), true);
                owner.Notice = "已取格 U=" + u + "，V=" + -row + "；更新候选检查静态约束。";
                input.Use(); return true;
            }
            if (owner.State.Page == "minerals" && owner.Preview.IsCandidate && owner.Generation.RulePlan != null)
            {
                int u = Mathf.Clamp(Mathf.RoundToInt((input.mousePosition.x - bounds.x) / bounds.width * 320), 0, 319);
                int row = Mathf.Clamp(Mathf.RoundToInt((input.mousePosition.y - bounds.y) / bounds.height * 192), 0, 191);
                owner.State.SelectedRegion = Core.Logic.Terrain.CaveRuleContent.Nearest(owner.Generation.RulePlan, u, row);
                owner.RebuildPage(); input.Use(); return true;
            }
            if (!owner.State.Sites || owner.Preview.Displayed == null ||
                owner.Preview.Canvas.ActiveTool != TerrainStylePreviewTool.Pan) return false;
            foreach (var site in owner.Preview.Displayed.Nests)
                if (Vector2.Distance(input.mousePosition, Point(bounds, site.U, site.Row)) <= 16)
                {
                    owner.State.SelectedNest = site.Id; owner.State.Extra = "nest";
                    owner.SelectPage("extras"); input.Use(); return true;
                }
            foreach (var site in owner.Preview.Displayed.GeologySites)
                if (Vector2.Distance(input.mousePosition, Point(bounds, site.AllowedU, -site.AllowedV)) <= 18)
                {
                    owner.State.SelectedSite = site.SiteId; owner.State.Extra = "collapse";
                    owner.SelectPage("extras"); input.Use(); return true;
                }
            return false;
        }

        internal void Draw(Rect clip, Rect bounds)
        {
            var map = owner.Preview.Displayed;
            if (map == null || Event.current.type != EventType.Repaint) return;
            GUI.BeginClip(clip); bounds.position -= clip.position;
            foreach (var site in map.Nests)
            {
                var p = Point(bounds, site.U, site.Row);
                EditorGUI.DrawRect(new Rect(p.x - 4, p.y - 4, 8, 8), new Color(.95f, .76f, .35f));
                GUI.Label(new Rect(p.x + 6, p.y - 8, 110, 22), "巢 " + site.Id, EditorStyles.whiteMiniLabel);
            }
            foreach (var site in map.GeologySites)
            {
                var p = Point(bounds, site.AllowedU, -site.AllowedV);
                EditorGUI.DrawRect(new Rect(p.x - 4, p.y - 4, 8, 8), new Color(.9f, .65f, .42f));
                GUI.Label(new Rect(p.x + 6, p.y - 8, 160, 22), site.SiteId, EditorStyles.whiteMiniLabel);
            }
            GUI.EndClip();
        }

        internal void DrawRegion(Rect clip, Rect bounds)
        {
            var plan = owner.Generation.RulePlan;
            if (owner.State.Page != "minerals" || plan == null || Event.current.type != EventType.Repaint) return;
            int selected = Mathf.Clamp(owner.State.SelectedRegion, 0, plan.Rooms.Count - 1);
            var room = plan.Rooms[selected];
            GUI.BeginClip(clip); bounds.position -= clip.position;
            Vector2 top = Point(bounds, room.Left, room.Top), bottom = Point(bounds, room.Left + room.Width, room.Top + room.Height);
            var rect = Rect.MinMaxRect(top.x, top.y, bottom.x, bottom.y);
            var color = new Color(.35f, .95f, .75f, .9f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 2, rect.y, 2, rect.height), color);
            GUI.Label(new Rect(top.x, top.y - 21, 110, 20), "所选区域 R" + selected, EditorStyles.whiteMiniLabel);
            GUI.EndClip();
        }
    }
}
