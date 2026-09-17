using System;
using DarkNights.Core.Config.Terrain;
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
                    GUI.Label(new Rect(12, 10, 400, 24), "F1 参数面板 · WASD 飞行 · Shift 加速 · 滚轮缩放");
                    Bootstrap.Flyer.PointerOverPanel = false;
                    return;
                }
                var area = new Rect(12, 12, 300, Mathf.Min(660, Screen.height / scale - 24));
                Bootstrap.Flyer.PointerOverPanel = area.Contains(Event.current.mousePosition);
                GUILayout.BeginArea(area, GUI.skin.box);
                scroll = GUILayout.BeginScrollView(scroll);
                GUILayout.BeginVertical(GUILayout.Width(266));
                GUILayout.Label("随机地图 · Debug Bootstrap");
                GUILayout.Label("WASD 穿墙飞行 / Shift 3×\nF 返回入口 / R 换种子\n滚轮缩放 / F1 收起面板", GUILayout.Height(60));
                var settings = Bootstrap.Settings;
                GUILayout.Label("种子");
                GUI.SetNextControlName("TerrainSeed");
                settings.Seed = GUILayout.TextField(settings.Seed ?? "", 80, GUILayout.Width(266));
                Bootstrap.Flyer.InputBlocked = GUI.GetNameOfFocusedControl() == "TerrainSeed";
                if (Event.current.type == EventType.MouseDown && !area.Contains(Event.current.mousePosition)) GUI.FocusControl(null);
                int surface = Math.Max(0, Array.IndexOf(TerrainGenerationSettings.SurfaceNames, settings.Surface));
                settings.Surface = TerrainGenerationSettings.SurfaceNames[GUILayout.SelectionGrid(surface, Surfaces, 3)];
                settings.OrganicCaves = GUILayout.Toggle(settings.OrganicCaves, "叠加自然洞穴");
                settings.Amplitude = Slider("地表起伏", (float)settings.Amplitude, .3f, 1.6f);
                settings.OreDensity = Slider("矿脉密度", (float)settings.OreDensity, .2f, 2);
                Bootstrap.LiveRegenerate = GUILayout.Toggle(Bootstrap.LiveRegenerate, "修改参数后实时重建");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("重建同种子")) { GUI.FocusControl(null); Bootstrap.RequestRegenerate(); }
                if (GUILayout.Button("新随机种子")) { GUI.FocusControl(null); Bootstrap.NewSeed(); }
                GUILayout.EndHorizontal();
                Bootstrap.Flyer.CameraDistance = Slider("镜头距离（越小越近）", Bootstrap.Flyer.CameraDistance, 5, 100);
                Bootstrap.Flyer.Speed = Slider("飞行速度（格/秒）", Bootstrap.Flyer.Speed, 2, 100);
                GUILayout.Label("房间定位");
                for (int i = 0; i < 2; i++)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = 0; j < 4; j++)
                    {
                        int room = i * 4 + j;
                        if (GUILayout.Button(Rooms[room])) { GUI.FocusControl(null); Bootstrap.VisitRoom(room); }
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
    }
}
