using DarkNights.Runtime.Objects;
using DarkNights.View.Expedition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 编辑态航程示意播放器；复用正式星点位移及淡出策略，只用本地编辑器时间和固定星点分布。
    /// 不创建游戏会话、场景对象或 GPU 资源，暂停及手动进度不会触碰权威航程状态。
    /// </summary>
    public sealed class TerrainJourneyPreview
    {
        public float Progress { get; private set; }
        public bool Playing { get; private set; }
        private double last;
        public void Play() { Progress = 0; Playing = true; last = EditorApplication.timeSinceStartup; }
        public void Stop() => Playing = false;
        public void Seek(float progress) { Progress = Mathf.Clamp01(progress); Stop(); }

        public void Tick(PlanetPreset planet)
        {
            double now = EditorApplication.timeSinceStartup;
            if (Playing && planet != null)
            {
                Progress = Mathf.Clamp01(Progress + (float)(now - last) / Mathf.Max(.01f, planet.TransitSeconds));
                if (Progress >= 1) Stop();
            }
            last = now;
        }

        public Vector2 StarPosition(PlanetPreset planet, int index)
        {
            var origin = new Vector2(Mathf.Repeat(index * .61803399f + .17f, 1), Mathf.Repeat(index * .41421356f + .31f, 1));
            return new ConfiguredTravelTransition(planet.TransitionKind).StarPosition(origin,
                Progress * Mathf.Max(.01f, planet.TransitSeconds), planet.StarSpeed);
        }

        public void Draw(Rect area, PlanetPreset planet)
        {
            GUI.BeginGroup(area);
            try
            {
                if (planet == null) { GUI.Label(new Rect(10, 10, area.width - 20, 30), "请先选择星球。"); return; }
                ColorUtility.TryParseHtmlString(planet.SpaceColorHex, out var space);
                ColorUtility.TryParseHtmlString(planet.SkyColorHex, out var sky);
                EditorGUI.DrawRect(new Rect(0, 0, area.width, area.height), Progress >= 1 ? sky : space);
                var strategy = new ConfiguredTravelTransition(planet.TransitionKind);
                float opacity = Progress >= 1 || planet.TransitionKind == "none" ? 0 :
                    strategy.StarOpacity(Progress * planet.TransitSeconds, planet.TransitSeconds);
                for (int index = 0; index < Mathf.Clamp(planet.StarCount, 0, 512); index++)
                {
                    var point = StarPosition(planet, index);
                    EditorGUI.DrawRect(new Rect(point.x * area.width, point.y * area.height, 2, 2),
                        new Color(.8f, .87f, 1, opacity * (.3f + index % 5 * .14f)));
                }
                GUI.Box(new Rect(area.width * .5f - 58, area.height * .5f - 18, 116, 36), "远征飞船");
                GUI.Label(new Rect(12, 10, area.width - 24, 24), "航程示意 · 星点采用正式过场策略", EditorStyles.whiteLabel);
                GUI.Label(new Rect(12, area.height - 30, area.width - 24, 24),
                    Progress >= 1 ? "到达 " + planet.DisplayName : "太空 → " + planet.DisplayName, EditorStyles.whiteLabel);
            }
            finally { GUI.EndGroup(); }
        }
    }
}
