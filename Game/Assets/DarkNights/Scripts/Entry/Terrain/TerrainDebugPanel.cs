using System;
using DarkNights.View.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkNights.Entry.Terrain
{
    /// <summary>离线工作台左栏；按地图、岩壁、背景、显示和状态分页，提前仲裁输入，调参及地图编辑均保留独立草稿语义。</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class TerrainDebugPanel : MonoBehaviour
    {
        public TerrainDebugBootstrap Bootstrap;
        public Font Font;
        public int ActiveTab { get; set; }
        public float UiScale = 1, PanelWidth = 380;
        public bool Visible = true;
        public int MapTool;
        public byte FillMaterial = 1;
        public bool ShowGrid;
        public string Message { get; private set; } = "更改先预览；应用保留本次运行，保存资产才写盘。";
        public TerrainPanelLayout Layout => new TerrainPanelLayout(Screen.width, Screen.height, UiScale, PanelWidth);
        private readonly Vector2[] scroll = new Vector2[5];
        private readonly TerrainStyleControls styleControls = new TerrainStyleControls();
        private readonly TerrainWorkbenchPages pages = new TerrainWorkbenchPages();
        private TerrainWorkbenchPointer pointer;
        private Font runtimeFont;
        private GUISkin skin;
        private bool textFocus;
        private static readonly string[] Tabs = { "地图", "岩壁", "背景", "显示", "状态" };

        private void OnEnable()
        {
            runtimeFont = UnityEngine.Font.CreateDynamicFontFromOSFont(
                Font != null ? Font.fontNames : new[] { "Microsoft YaHei", "Arial" }, 15);
            pointer = new TerrainWorkbenchPointer();
            if (Bootstrap != null) Bootstrap.StyleDraft = new CaveStyleDraft(Bootstrap.CaveStyle);
        }
        private void Update()
        {
            if (Bootstrap?.Flyer == null) return;
            if (Keyboard.current?.f1Key.wasPressedThisFrame == true)
            { Visible = !Visible; textFocus = false; pointer.Stop(); }
            var mousePoint = Mouse.current?.position.ReadValue() ?? new Vector2(-1, -1);
            bool over = Mouse.current != null && (Visible ? Layout.ContainsScreenPoint(mousePoint, Screen.height) :
                new Rect(12, 12, 200, 32).Contains(new Vector2(mousePoint.x, Screen.height - mousePoint.y) / Layout.Scale));
            Bootstrap.Flyer.PointerOverPanel = over;
            if (Mouse.current?.leftButton.wasPressedThisFrame == true && !over) textFocus = false;
            Bootstrap.Flyer.InputBlocked = textFocus;
            Bootstrap.Flyer.WorkbenchPointerActive = MapTool != 0;
            pointer.Update(this, over || textFocus || Bootstrap.Generating);
            pages.Observe(Bootstrap);
        }
        private void OnGUI()
        {
            if (Bootstrap?.Flyer == null) return;
            var previousSkin = GUI.skin; var previousMatrix = GUI.matrix; bool previousEnabled = GUI.enabled;
            EnsureSkin(previousSkin);
            var layout = Layout;
            GUI.skin = skin; GUI.matrix = Matrix4x4.Scale(new Vector3(layout.Scale, layout.Scale, 1));
            try
            {
                pointer.DrawGrid(this, layout.Scale);
                if (!Visible)
                {
                    if (GUI.Button(new Rect(12, 12, 200, 32), "F1 · 打开地图工作台")) Visible = true;
                    return;
                }
                GUILayout.BeginArea(layout.Panel, skin.box);
                GUILayout.BeginHorizontal(); GUILayout.Label("洞穴地图工作台", skin.GetStyle("title"));
                if (GUILayout.Button("收起", GUILayout.Width(52))) { Visible = false; textFocus = false; pointer.Stop(); }
                GUILayout.EndHorizontal();
                GUILayout.Label(Bootstrap.FixedMap != null ? "固定地图 · " + Bootstrap.FixedMap.name : "随机地图 · " + Bootstrap.Settings.Seed, skin.GetStyle("hint"));
                int selected = GUILayout.SelectionGrid(ActiveTab, Tabs, layout.TabColumns);
                if (selected != ActiveTab)
                { ActiveTab = selected; textFocus = false; pointer.Stop(); GUI.FocusControl(null); GUIUtility.ExitGUI(); }
                scroll[ActiveTab] = GUILayout.BeginScrollView(scroll[ActiveTab], false, false);
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                if (ActiveTab == 0) pages.DrawMap(this, styleControls);
                else if (ActiveTab == 1 || ActiveTab == 2) styleControls.Draw(this, ActiveTab == 2);
                else if (ActiveTab == 3) pages.DrawDisplay(this);
                else pages.DrawStatus(this);
                GUILayout.EndVertical(); GUILayout.EndScrollView();
                if (ActiveTab == 1 || ActiveTab == 2) DrawStyleActions();
                GUILayout.Label(Bootstrap.Generating ? "正在更新地图表现…" : Message, skin.GetStyle("hint"));
                GUILayout.EndArea();
                textFocus = !string.IsNullOrEmpty(GUI.GetNameOfFocusedControl());
                if (Event.current.type == EventType.MouseDown && !layout.Panel.Contains(Event.current.mousePosition))
                { GUI.FocusControl(null); textFocus = false; }
            }
            finally { GUI.matrix = previousMatrix; GUI.skin = previousSkin; GUI.enabled = previousEnabled; }
        }
        private void DrawStyleActions()
        {
            var draft = Bootstrap.StyleDraft;
            if (draft?.Style == null) return;
            bool enabled = GUI.enabled; GUI.enabled = enabled && draft.HasChanges && !Bootstrap.Generating;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("应用样式")) Run(() => { draft.Apply(); SetMessage("样式已应用到本次运行；退出 Play 不保存资产。"); });
            if (GUILayout.Button("取消样式")) Run(() => { draft.Cancel(); StyleChanged(); SetMessage("已恢复上次应用的样式。"); });
            GUILayout.EndHorizontal(); GUI.enabled = enabled && !Bootstrap.Generating;
            if (TerrainWorkbenchAssets.SaveStyle != null && GUILayout.Button("保存样式资产"))
                Run(() => { TerrainWorkbenchAssets.SaveStyle(draft); draft.Apply(); SetMessage("已保存样式资产，共享此样式的场景也会使用修改。"); });
            GUI.enabled = enabled;
        }
        private void EnsureSkin(GUISkin source)
        {
            if (skin != null) return;
            skin = Instantiate(source); skin.font = runtimeFont;
            foreach (var style in new[] { skin.label, skin.button, skin.toggle, skin.textField, skin.box })
            { style.fontSize = 14; style.wordWrap = true; }
            skin.button.padding = new RectOffset(8, 8, 6, 6); skin.button.fixedHeight = 0;
            skin.textField.padding = new RectOffset(6, 6, 5, 5);
            skin.box.padding = new RectOffset(10, 10, 8, 8);
            skin.verticalScrollbar.fixedWidth = 16;
            skin.customStyles = new[] {
                new GUIStyle(skin.label) { name = "title", fontSize = 18, fontStyle = FontStyle.Bold },
                new GUIStyle(skin.label) { name = "hint", fontSize = 12, wordWrap = true }
            };
        }
        public void StyleChanged()
        { Bootstrap.RequestStyleRefresh(); SetMessage("草稿预览 · 稍停后更新；地图拆填和角色位置保留。"); }
        public void SetMessage(string message) => Message = message;
        public void Run(Action action)
        { try { action(); } catch (Exception error) { SetMessage(error.Message); } }
        public void SwitchStyle(CaveTerrainStyle style)
        {
            if (style == null) return;
            if (Bootstrap.StyleDraft?.HasChanges == true) { SetMessage("先应用或取消当前样式草稿。"); return; }
            Bootstrap.StyleDraft?.Dispose(); Bootstrap.CaveStyle = style; Bootstrap.StyleDraft = new CaveStyleDraft(style); StyleChanged();
        }
        private void OnApplicationFocus(bool focus) { if (!focus) { pointer?.Stop(); textFocus = false; } }
        private void OnDisable()
        {
            pointer?.Stop();
            if (Bootstrap != null)
            {
                Bootstrap.StyleDraft?.Dispose(); Bootstrap.StyleDraft = null;
                if (Bootstrap.Flyer != null)
                { Bootstrap.Flyer.InputBlocked = false; Bootstrap.Flyer.PointerOverPanel = false; Bootstrap.Flyer.WorkbenchPointerActive = false; }
            }
            CaveStyleDraft.Release(runtimeFont); CaveStyleDraft.Release(skin); runtimeFont = null; skin = null;
        }
    }
}
