using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.View.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkNights.Entry.Terrain
{
    /// <summary>
    /// 独立调试场景的即时参数面板，参数变化交由 Bootstrap 防抖重建，房间按钮仅移动本地观察角色。
    /// 文本编辑时屏蔽飞行和重生快捷键；不调用正式会话命令，也不读写玩家存档。
    /// </summary>
    public sealed class TerrainDebugPanel : MonoBehaviour
    {
        public TerrainDebugBootstrap Bootstrap;
        public Font Font;
        private bool visible = true;
        private Vector2 scroll;
        private Font runtimeFont;
        private GUISkin skin;
        private static readonly string[] Surfaces = { "针峰", "台地", "喀斯特", "盆地", "丘陵", "断层" };
        private static readonly string[] Rooms = { "入口", "矿洞", "树根", "长廊", "熔炉", "首领", "密室", "遗迹" };

        private void OnEnable()
        {
            // 序列化的 UIFont 不含字体数据；IMGUI 的字体引擎需要实际加载的系统字面。
            runtimeFont = UnityEngine.Font.CreateDynamicFontFromOSFont(
                Font != null ? Font.fontNames : new[] { "Microsoft YaHei", "Arial" }, 15);
        }

        private void OnDisable()
        {
            if (runtimeFont != null) Destroy(runtimeFont);
            if (skin != null) Destroy(skin);
            if (Bootstrap != null && Bootstrap.Flyer != null)
            {
                Bootstrap.Flyer.InputBlocked = false;
                Bootstrap.Flyer.PointerOverPanel = false;
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                visible = !visible;
                Bootstrap.Flyer.InputBlocked = false;
            }
        }

        private void OnGUI()
        {
            if (Bootstrap == null || Bootstrap.Flyer == null) return;
            GUISkin previous = GUI.skin;
            if (skin == null)
            {
                skin = Instantiate(previous); skin.font = runtimeFont;
                skin.label.wordWrap = true; skin.box.wordWrap = true;
            }
            GUI.skin = skin;
            float scale = Mathf.Max(1, Screen.height / 900f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            try
            {
                if (!visible)
                {
                    GUI.Label(new Rect(12, 10, 620, 24), Bootstrap.Workshop == null ? "F1 参数 · WASD 观察 · 滚轮缩放" :
                        "F1 参数 · Tab 行走/观察 · AD 移动 · 空格跳跃/喷气 · 滚轮缩放");
                    Bootstrap.Flyer.PointerOverPanel = false;
                    return;
                }
                var area = new Rect(12, 12, 300, Mathf.Min(660, Screen.height / scale - 24));
                Bootstrap.Flyer.PointerOverPanel = area.Contains(Event.current.mousePosition);
                GUILayout.BeginArea(area, GUI.skin.box);
                scroll = GUILayout.BeginScrollView(scroll);
                GUILayout.BeginVertical(GUILayout.Width(266));
                GUILayout.Label(Bootstrap.Workshop == null ? "随机地图 · Debug Bootstrap" : "天然洞穴 · 地图工作台");
                if (Bootstrap.Workshop != null) GUILayout.Label("喷气燃料：" + Bootstrap.Workshop.Fuel.ToString("0.0") + " 秒（落地恢复）");
                GUILayout.Label(Bootstrap.Workshop == null ? "WASD 穿墙飞行 / Shift 3×\nF 返回入口 / R 换种子\n滚轮缩放 / F1 收起面板" :
                    "Tab 行走/穿墙观察 · AD 移动\n空格跳跃/按住喷气 · F 回入口\n左键手采 / 右键调试爆破（6格）\nR 换种子 · F1 面板 · 滚轮缩放", GUILayout.Height(85));
                var settings = Bootstrap.Settings;
                GUILayout.Label("种子");
                GUI.SetNextControlName("TerrainSeed");
                settings.Seed = GUILayout.TextField(settings.Seed ?? "", 80, GUILayout.Width(266));
                Bootstrap.Flyer.InputBlocked = GUI.GetNameOfFocusedControl() == "TerrainSeed";
                if (Event.current.type == EventType.MouseDown && !area.Contains(Event.current.mousePosition)) GUI.FocusControl(null);
                bool cave = settings.ResourceProfile == TerrainGenerationSettings.CaveExplorationProfile;
                if (cave)
                {
                    GUILayout.Label("参考 HTML 紧凑洞穴 · 设计层级 / 受控连接");
                    DrawCaveGeneration(settings);
                    DrawCaveStyle(Bootstrap.EditableCaveStyle);
                }
                else
                {
                    int surface = Math.Max(0, Array.IndexOf(TerrainGenerationSettings.SurfaceNames, settings.Surface));
                    settings.Surface = TerrainGenerationSettings.SurfaceNames[GUILayout.SelectionGrid(surface, Surfaces, 3)];
                    settings.OrganicCaves = GUILayout.Toggle(settings.OrganicCaves, "叠加自然洞穴");
                    settings.OreDensity = Slider("矿脉密度", (float)settings.OreDensity, .2f, 2);
                }
                settings.Amplitude = Slider("地表起伏", (float)settings.Amplitude, .3f, 1.6f);
                Bootstrap.LiveRegenerate = GUILayout.Toggle(Bootstrap.LiveRegenerate, "修改参数后实时重建");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("重建同种子")) { GUI.FocusControl(null); Bootstrap.RequestRegenerate(); }
                if (GUILayout.Button("新随机种子")) { GUI.FocusControl(null); Bootstrap.NewSeed(); }
                GUILayout.EndHorizontal();
                Bootstrap.Flyer.CameraDistance = Slider("镜头距离（越小越近）", Bootstrap.Flyer.CameraDistance, 5, 100);
                Bootstrap.Flyer.Speed = Slider("飞行速度（格/秒）", Bootstrap.Flyer.Speed, 2, 100);
                GUILayout.Label("房间定位");
                for (int i = 0; i < ((Bootstrap.Blueprint?.Rooms.Count ?? 8) + 3) / 4; i++)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = 0; j < 4; j++)
                    {
                        int room = i * 4 + j;
                        if (room >= (Bootstrap.Blueprint?.Rooms.Count ?? 8)) break;
                        string label = cave ? (room == 0 ? "入口" : "洞室 " + room) : Rooms[room];
                        if (GUILayout.Button(label)) { GUI.FocusControl(null); Bootstrap.VisitRoom(room); }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.Label(Bootstrap.Status, GUI.skin.box, GUILayout.Height(52));
                Vector3 p = Bootstrap.Flyer.transform.position;
                GUILayout.Label($"格子：{p.x:F1}, {-p.y:F1} · 全图 320×192", GUILayout.Height(24));
                GUILayout.EndVertical(); GUILayout.EndScrollView(); GUILayout.EndArea();
            }
            finally { GUI.matrix = previousMatrix; GUI.skin = previous; }
        }

        private static float Slider(string label, float value, float min, float max)
        {
            GUILayout.Label(label + "  " + value.ToString("F2"));
            return GUILayout.HorizontalSlider(value, min, max);
        }

        private static void DrawCaveGeneration(TerrainGenerationSettings settings)
        {
            GUILayout.Label("空间参数", GUI.skin.box);
            settings.CaveColumnSpacing = Mathf.RoundToInt(Slider("横向洞室间距", settings.CaveColumnSpacing, 34, 60));
            settings.CaveRowSpacing = Mathf.RoundToInt(Slider("纵向层距", settings.CaveRowSpacing, 18, 36));
            settings.CaveRoomWidthScale = Slider("洞室宽度比例", (float)settings.CaveRoomWidthScale, .45f, 1.25f);
            settings.CaveRoomHeightScale = Slider("洞室高度比例", (float)settings.CaveRoomHeightScale, .45f, 1.25f);
            settings.CavePassageRadius = Mathf.RoundToInt(Slider("通路半径（最小 2）", settings.CavePassageRadius, 2, 4));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("参考紧凑")) settings.UseCompactCaveDefaults();
            if (GUILayout.Button("旧版宽松")) settings.UseLegacyCaveScale();
            GUILayout.EndHorizontal();
        }

        private void DrawCaveStyle(CaveTerrainStyle style)
        {
            if (style == null) return;
            GUILayout.Label("材质参数（即时，不重建地图）", GUI.skin.box);
            bool changed = false;
            int seed = Mathf.RoundToInt(Slider("纹理种子", style.TextureSeed, 0, 255));
            changed |= seed != style.TextureSeed; style.TextureSeed = seed;
            changed |= Set(ref style.TextureDetail, Slider("纹理密度", style.TextureDetail, 0, .6f));
            changed |= Set(ref style.EdgeStrength, Slider("边缘提亮强度", style.EdgeStrength, 0, .6f));
            changed |= Set(ref style.EdgeStartPixels, Slider("边缘起始（像素）", style.EdgeStartPixels, 1, 5));
            changed |= Set(ref style.EdgeDecayPixels, Slider("衰减过渡（像素）", style.EdgeDecayPixels, 4, 16));
            changed |= Set(ref style.CoreAfterPixels, Slider("近黑岩芯深度", style.CoreAfterPixels, 16, 48));
            changed |= Set(ref style.EdgeSoftness, Slider("岩壁衰减柔度", style.EdgeSoftness, .5f, 2.5f));
            changed |= Set(ref style.LightSoftness, Slider("灯光柔度", style.LightSoftness, .5f, 2.5f));
            changed |= Set(ref style.LightFalloff, Slider("空气光衰减（越小越远）", style.LightFalloff, 6, 24));
            changed |= Set(ref style.RockLightLoss, Slider("岩体遮光损耗", style.RockLightLoss, 48, 160));
            changed |= Set(ref style.DarkColor, ColorSliders("暗部", style.DarkColor));
            changed |= Set(ref style.BaseColor, ColorSliders("基色", style.BaseColor));
            changed |= Set(ref style.LightColor, ColorSliders("亮部", style.LightColor));
            changed |= Set(ref style.EdgeColor, ColorSliders("边缘色", style.EdgeColor));
            if (GUILayout.Button("恢复素材默认参数")) { Bootstrap.ResetCaveStyle(); changed = false; }
            else if (changed) Bootstrap.ApplyCaveStyle();
        }

        private static Color ColorSliders(string label, Color value)
        {
            GUILayout.Label(label + $"  #{ColorUtility.ToHtmlStringRGB(value)}");
            GUILayout.BeginHorizontal();
            GUILayout.Label("R", GUILayout.Width(14)); value.r = GUILayout.HorizontalSlider(value.r, 0, 1);
            GUILayout.Label("G", GUILayout.Width(14)); value.g = GUILayout.HorizontalSlider(value.g, 0, 1);
            GUILayout.Label("B", GUILayout.Width(14)); value.b = GUILayout.HorizontalSlider(value.b, 0, 1);
            GUILayout.EndHorizontal(); value.a = 1; return value;
        }

        private static bool Set(ref float target, float value)
        {
            if (Mathf.Approximately(target, value)) return false;
            target = value; return true;
        }

        private static bool Set(ref Color target, Color value)
        {
            if (target == value) return false;
            target = value; return true;
        }
    }
}
