using System;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// Tuner 折叠分组与固定操作区；序列化分类、目录和预览选择，原生 Inspector 继续拥有样式编辑。
    /// 地图与航程、表现分别显式保存；临时拆填始终留在地图草稿，不包含在任何资产保存动作中。
    /// </summary>
    [Serializable]
    public sealed class TerrainWorkbenchControls : IDisposable
    {
        [SerializeField] private string section = "地图生成";
        [SerializeField] private bool catalog;
        [SerializeField] private bool journeyView;
        [SerializeField] private bool landingMarkers = true;
        [NonSerialized] private TerrainPlanetControls form;
        private static readonly GUIContent LogicalGrid = new GUIContent("1 逻辑网格", "快捷键 1（含小键盘）：显示青色逻辑网格。拆填按整数逻辑格中心选取。");
        private static readonly GUIContent RenderGrid = new GUIContent("2 渲染网格", "快捷键 2（含小键盘）：显示金色渲染网格，与逻辑网格错开半格；拆填仍操作逻辑格。");
        public bool JourneyView => journeyView;
        public bool LandingMarkers => landingMarkers;
        public string Section => section;
        public void Bind(TerrainGenerationPreview model) { form?.Dispose(); form = new TerrainPlanetControls(model); }
        public void FocusJourney() { section = "航程设置"; journeyView = false; }

        public void DrawHeader(Rect area, TerrainGenerationPreview model, Action changed)
        {
            GUILayout.BeginArea(area); EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("星球", GUILayout.Width(30)); form.DrawPicker(changed);
            if (GUILayout.Button("管理星球", EditorStyles.toolbarButton, GUILayout.Width(75)))
            { catalog = !catalog; section = "星球与降落"; }
            GUILayout.FlexibleSpace(); GUILayout.Label("配置来源：WorldSession", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal(); GUILayout.EndArea();
        }

        public void DrawSidebar(TerrainGenerationPreview model, CaveTerrainStyle style, TerrainStyleDrafts drafts,
            ref ScriptableObject inspected, TerrainMapDraft map, Action changed, Action styleChanged, Action play)
        {
            if (catalog)
            {
                using (new EditorGUI.DisabledScope(map.HasChanges)) form.DrawCatalog(changed);
                EditorGUILayout.Space();
            }
            if (Fold("地图生成"))
            {
                if (map.HasChanges) EditorGUILayout.HelpBox("先取消或重置临时拆填，再修改生成参数。", MessageType.Info);
                using (new EditorGUI.DisabledScope(map.HasChanges)) form.DrawMap(changed);
                foreach (var item in model.Diagnostics)
                    EditorGUILayout.LabelField(item.StableId + " · " + item.Stage + " · 改动 " + item.ChangedCells + " 格",
                        EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.Space();
            if (Fold("岩壁与背景"))
            {
                inspected = drafts.ChooseAsset(style, inspected);
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("原资产", inspected, typeof(ScriptableObject), false);
                if (inspected != null) drafts.DrawInspector(inspected, styleChanged);
            }
            EditorGUILayout.Space();
            if (Fold("星球与降落")) using (new EditorGUI.DisabledScope(map.HasChanges)) form.DrawLanding(changed);
            EditorGUILayout.Space();
            if (Fold("航程设置")) form.DrawJourney(changed, () => { journeyView = true; play(); });
            try { model.Draft.Config.Validate(); }
            catch (Exception error) { EditorGUILayout.HelpBox("草稿待修正：" + error.Message, MessageType.Warning); }
        }

        private bool Fold(string name)
        {
            bool open = section == name;
            bool next = EditorGUILayout.Foldout(open, name, true);
            if (next != open) section = next ? name : "";
            return next;
        }

        public void DrawCanvasTools(Rect area, TerrainStylePreviewCanvas preview, ref byte fillMaterial)
        {
            GUILayout.BeginArea(area); EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(journeyView))
            {
                preview.ActiveTool = (TerrainStylePreviewTool)GUILayout.Toolbar((int)preview.ActiveTool,
                    new[] { "平移", "拆", "填" }, GUILayout.Width(90));
                if (preview.ActiveTool == TerrainStylePreviewTool.Fill)
                    fillMaterial = (byte)(EditorGUILayout.Popup(fillMaterial - 1,
                        new[] { "壤土", "板岩", "玄武岩", "铜矿", "铁矿", "金矿", "苔岩" }, GUILayout.Width(55)) + 1);
                if (area.width >= 420)
                    landingMarkers = GUILayout.Toggle(landingMarkers, "降落标记", EditorStyles.toolbarButton, GUILayout.Width(60));
                GUILayout.FlexibleSpace();
                if (area.width >= 570)
                {
                    float zoom = GUILayout.HorizontalSlider(preview.Zoom, .5f, 8, GUILayout.Width(55));
                    if (!Mathf.Approximately(zoom, preview.Zoom)) preview.SetZoom(zoom, Vector2.zero, false);
                }
                GUILayout.Label(preview.Zoom.ToString("0.0") + "×", GUILayout.Width(28));
                if (GUILayout.Button("适配", EditorStyles.toolbarButton, GUILayout.Width(36))) preview.Fit();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(journeyView))
            {
                preview.ShowGrid = GUILayout.Toggle(preview.ShowGrid, "网格", EditorStyles.toolbarButton, GUILayout.Width(38));
                DrawGridButton(preview, TerrainGridMode.Logical, LogicalGrid, new Color(.45f, .85f, 1f));
                DrawGridButton(preview, TerrainGridMode.Render, RenderGrid, new Color(.87f, .73f, .40f));
                GUILayout.FlexibleSpace();
                if (area.width >= 460)
                    GUILayout.Label("拆填始终按逻辑格", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal(); GUILayout.EndArea();
        }

        private static void DrawGridButton(TerrainStylePreviewCanvas preview, TerrainGridMode mode, GUIContent label, Color tint)
        {
            bool selected = preview.ShowGrid && preview.GridMode == mode;
            Color original = GUI.backgroundColor;
            GUI.backgroundColor = selected ? tint : original;
            bool next = GUILayout.Toggle(selected, label, EditorStyles.toolbarButton, GUILayout.Width(92));
            GUI.backgroundColor = original;
            if (next && !selected) preview.SelectGrid(mode);
        }

        public void DrawPreviewBar(Rect area, TerrainJourneyPreview journey, TerrainMapDraft map, Action changed, Action reset)
        {
            GUILayout.BeginArea(area); EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Toggle(!journeyView, "地图预览", EditorStyles.toolbarButton, GUILayout.Width(72))) journeyView = false;
            if (GUILayout.Toggle(journeyView, "航程示意", EditorStyles.toolbarButton, GUILayout.Width(72))) journeyView = true;
            if (journeyView)
            {
                if (GUILayout.Button(journey.Playing ? "暂停" : "播放", EditorStyles.toolbarButton, GUILayout.Width(44)))
                { if (journey.Playing) journey.Stop(); else journey.Play(); }
                EditorGUI.BeginChangeCheck(); float progress = GUILayout.HorizontalSlider(journey.Progress, 0, 1);
                if (EditorGUI.EndChangeCheck()) journey.Seek(progress);
                GUILayout.Label((journey.Progress * 100).ToString("0") + "%", GUILayout.Width(34));
            }
            else
            {
                GUILayout.Label("临时拆填 " + map.ChangedCells + " 格", EditorStyles.miniLabel); GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!map.CanUndo)) if (GUILayout.Button("撤销", EditorStyles.toolbarButton)) { map.Undo(); changed(); }
                using (new EditorGUI.DisabledScope(!map.CanRedo)) if (GUILayout.Button("重做", EditorStyles.toolbarButton)) { map.Redo(); changed(); }
                using (new EditorGUI.DisabledScope(!map.HasChanges)) if (GUILayout.Button("取消拆填", EditorStyles.toolbarButton)) reset();
            }
            EditorGUILayout.EndHorizontal(); GUILayout.EndArea();
        }

        public void DrawFooter(Rect area, TerrainGenerationPreview model, TerrainStyleDrafts drafts, string status,
            Action saveWorld, Action saveStyle, Action cancel)
        {
            GUILayout.BeginArea(area); EditorGUILayout.BeginHorizontal();
            GUILayout.Label("地图与航程：" + (model.HasChanges ? "未保存" : "已保存") + " · 表现：" +
                (drafts.HasChanges ? "未保存" : "已保存"), EditorStyles.miniLabel); GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!model.HasChanges)) if (GUILayout.Button("保存地图与航程")) saveWorld();
            using (new EditorGUI.DisabledScope(!drafts.HasChanges)) if (GUILayout.Button("保存表现")) saveStyle();
            using (new EditorGUI.DisabledScope(!model.HasChanges && !drafts.HasChanges)) if (GUILayout.Button("取消修改")) cancel();
            EditorGUILayout.EndHorizontal(); EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel); GUILayout.EndArea();
        }

        public void Dispose() { form?.Dispose(); form = null; }
    }
}
