using System;
using System.Collections.Generic;
using System.IO;
using DarkNights.Core.Config.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// Tuner当前真实地图的材料剖面画布；矿层先画、前景后画，可透视核对同坐标分层。
    /// 复用CL04原生PNG作被动出巢姿势，窗口关闭释放纹理；不导入、复制或改写唯一美术源。
    /// </summary>
    internal sealed class TerrainVeinStudyCanvas : IDisposable
    {
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly HashSet<string> unavailable = new HashSet<string>();
        private float unit;
        private Vector2 origin;
        private Color ore = new Color(.72f, .49f, .18f), warning = new Color(.76f, .32f, .36f);

        internal void Draw(Rect area, TerrainVeinStudyLayout layout, TerrainVeinStudyFrame frame, bool xray)
        {
            EditorGUI.DrawRect(area, EditorGUIUtility.isProSkin ? new Color(.12f, .14f, .14f) : new Color(.91f, .93f, .91f));
            unit = Mathf.Min((area.width - 24) / TerrainVeinStudyLayout.Width,
                (area.height - 62) / TerrainVeinStudyLayout.Height);
            if (unit <= 0) return;
            origin = new Vector2(area.center.x - TerrainVeinStudyLayout.Width * unit / 2, area.y + 32);
            GUI.Label(new Rect(area.x + 12, area.y + 6, area.width - 24, 22),
                "当前地图格级制作剖面 · 前景墙 / 背景矿 · 非运行验收", EditorStyles.miniLabel);
            for (int y = 0; y < TerrainVeinStudyLayout.Height; y++)
                for (int x = 0; x < TerrainVeinStudyLayout.Width; x++)
                {
                    if (layout.Capacity(x, y) == 0 || frame.Collected(x, y)) continue;
                    Tile(x, y, 1, 1, new Color(ore.r, ore.g, ore.b, .35f));
                    Tile(x + .08f, y + .08f, .86f, .84f, ore);
                    Tile(x + .25f, y + .18f, .32f, .15f, new Color(.88f, .73f, .43f));
                }
            for (int y = 0; y < TerrainVeinStudyLayout.Height; y++)
                for (int x = 0; x < TerrainVeinStudyLayout.Width; x++)
                {
                    bool pocket = layout.Pocket(x, y);
                    bool hiddenPocket = pocket && !frame.Opened && !xray;
                    if (!hiddenPocket && (pocket || frame.Removed(layout, x, y) || layout.Material(x, y) == 0)) continue;
                    float shade = .28f + (x * 7 + y * 3) % 4 * .025f;
                    Color rock = EditorGUIUtility.isProSkin ? new Color(shade, shade + .025f, shade + .01f, xray ? .38f : 1) :
                        new Color(.55f + shade / 3, .57f + shade / 3, .55f + shade / 3, xray ? .38f : 1);
                    Tile(x, y, 1, 1, rock); Tile(x + .06f, y + .07f, .88f, .04f, new Color(1, 1, 1, .08f));
                }
            if (layout.HasNest && (xray || frame.Opened)) Sprite("burrow/occupied", 9.8f, 9, false);
            if (frame.Time >= 5.5f && !frame.Opened && frame.Decision != 2)
                Tile(7.1f, 7.1f, .12f, 1.6f, warning);
            float playerX = 2 + Mathf.Min(4, frame.Time * .85f);
            float backing = frame.Decision == 2 ? Mathf.Max(0, frame.Time - 6) :
                frame.Opened ? Mathf.Max(0, frame.Time - 7.5f) : 0;
            playerX -= Mathf.Min(4, backing * 1.1f);
            DrawPlayer(playerX, 9);
            if (frame.Creature)
            {
                float t = frame.Time - 7.5f;
                string pose = t < .4f ? "alert" : ((int)(t * 8) % 2 == 0 ? "crawl_a" : "crawl_b");
                Sprite("ambush-grub/" + pose, 9.8f - Mathf.Min(6, Mathf.Max(0, t - .4f) * 1.4f), 9, true);
            }
            GUI.Label(new Rect(area.x + 12, area.yMax - 24, area.width - 24, 20),
                "源种子 " + layout.SourceSeed + " · 入口世界格 " + layout.WorldX + "," + layout.WorldY +
                (unavailable.Count > 0 ? " · 原生PNG缺失，姿势未显示" : ""), EditorStyles.miniLabel);
        }

        private void DrawPlayer(float x, float feet)
        {
            Color color = EditorGUIUtility.isProSkin ? new Color(.83f, .87f, .83f) : new Color(.15f, .22f, .2f);
            Tile(x - .18f, feet - 1.65f, .36f, .38f, color);
            Tile(x - .1f, feet - 1.25f, .2f, .8f, color);
            Tile(x - .25f, feet - .5f, .15f, .5f, color); Tile(x + .1f, feet - .5f, .15f, .5f, color);
            Tile(x, feet - 1.15f, .75f, .1f, color);
        }

        private void Tile(float x, float y, float width, float height, Color color) =>
            EditorGUI.DrawRect(new Rect(origin.x + x * unit, origin.y + y * unit, width * unit, height * unit), color);

        private void Sprite(string name, float x, float feet, bool flip)
        {
            if (!textures.TryGetValue(name, out Texture2D image))
            {
                if (unavailable.Contains(name)) return;
                string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../experiments/cl04-creature-art-v1/native", name + ".png"));
                if (!File.Exists(path)) { unavailable.Add(name); return; }
                image = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                { name = "Tuner " + name, filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                if (!image.LoadImage(File.ReadAllBytes(path)))
                { UnityEngine.Object.DestroyImmediate(image); unavailable.Add(name); return; }
                textures.Add(name, image);
            }
            // 原图32×24，16px/格；脚底锚点(16,23)，不拉伸整张图以适配巢腔。
            var rect = new Rect(origin.x + (x - 1) * unit, origin.y + (feet - 23f / 16) * unit, 2 * unit, 1.5f * unit);
            GUI.DrawTextureWithTexCoords(rect, image, flip ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1), true);
        }

        public void Dispose()
        {
            foreach (var image in textures.Values) if (image != null) UnityEngine.Object.DestroyImmediate(image);
            textures.Clear(); unavailable.Clear();
        }
    }
}
