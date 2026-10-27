using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.View.Expedition;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 从最终格子制作稳定地表装饰；只读原素材，局部坡形／洞口／泊位决定可落位范围。
    /// 仅在作者显式请求时生成新资产，不覆盖旧布局或在运行中随机；人工编辑继续归独立布局草稿。
    /// </summary>
    public static class SurfaceSceneryAuthoring
    {
        public const string Root = "Assets/DarkNights/Res/Terrain/SurfaceEnvironment";
        private const string Sprites = "Assets/DarkNights/Res/Art/Original/sprites/";

        public static SurfaceSceneryLayout Create(PlayableTerrain map, PlanetDefinition planet,
            CaveTerrainStyle style = null, TerrainStyleDrafts drafts = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || map?.Background == null)
                throw new InvalidOperationException("仅编辑态完整候选可制作装饰。");
            EnsureFolder(Root + "/Layouts");
            var layout = ScriptableObject.CreateInstance<SurfaceSceneryLayout>();
            layout.Recipe = SurfaceSceneryLayout.DefaultRecipe;
            layout.TerrainKey = SurfaceSceneryLayout.Key(map.Background);
            layout.Placements = Build(map, planet, layout, style, drafts);
            Validate(layout, map);
            string path = Root + "/Layouts/Surface-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".asset";
            if (File.Exists(path)) throw new IOException("装饰版本路径已存在。");
            AssetDatabase.CreateAsset(layout, path); AssetDatabase.SaveAssetIfDirty(layout);
            return layout;
        }

        public static SurfaceSceneryPlacement[] Build(PlayableTerrain map, PlanetDefinition planet, SurfaceSceneryLayout options,
            CaveTerrainStyle style = null, TerrainStyleDrafts drafts = null)
        {
            if (options.Density < 0 || options.Density > 1 || float.IsNaN(options.Density) ||
                options.TreeSpacing < 5 || options.TreeSpacing > 24 || options.StoneSpacing < 3 || options.StoneSpacing > 20)
                throw new ArgumentException("装饰密度或间距越界。");
            var entries = new List<SurfaceSceneryPlacement>();
            byte[] shapes = map.CopyShapes();
            bool[] protection = map.CopyProtection();
            uint seed = TerrainRandom.Hash(map.Seed + "|surface-scatter-v1");
            int[] rows = new int[TerrainGenerationSettings.Width];
            for (int x = 0; x < rows.Length; x++)
            {
                int row = 0;
                while (row < TerrainGenerationSettings.Height && map.Material(x, row) == 0) row++;
                rows[x] = row;
            }
            var ground = new SurfaceSceneryGround(map, style, drafts, Math.Min(190, planet.DockRow + 24));
            int lastTree = -100, lastStone = -100;
            int left = planet.DockColumn - planet.LandingWidth / 2 - 3, right = left + planet.LandingWidth + 5;
            for (int x = 4; x < rows.Length - 4; x++)
            {
                int row = rows[x];
                if (x >= left && x <= right || row < 2 || row > planet.DockRow + 24 || row >= 190 ||
                    protection[row * rows.Length + x] || shapes[row * rows.Length + x] >= 7) continue;
                bool gap = Math.Abs(rows[x - 1] - row) > 2 || Math.Abs(rows[x + 1] - row) > 2;
                bool outdoor = Enumerable.Range(x - 3, 7).All(c => Math.Abs(rows[c] - row) <= 3);
                if (gap || !outdoor || map.Material(x, row) != 1 && map.Material(x, row) != 7) continue;
                double noise = TerrainRandom.Noise(seed, x);
                bool flat = Enumerable.Range(x - 1, 3).All(c => rows[c] == row && shapes[row * rows.Length + c] == 0);
                bool gentle = Enumerable.Range(x - 1, 3).All(c => Math.Abs(rows[c] - row) <= 1);
                if (gentle && x - lastTree >= options.TreeSpacing && x - lastStone >= 4 && noise < options.Density * .75)
                {
                    int variant = (int)(TerrainRandom.Noise(seed + 1, x) * 3) % 3;
                    string family = TerrainRandom.Noise(seed + 4, x) < .8 ? "spr_res_trees" : "spr_res_trees_leaf";
                    var tree = Entry(shapes[row * rows.Length + x], x, row, "tree", Texture(family, variant), 2, 0);
                    tree.GroundOffsetPixels = ground.Offset(x, row, tree.Shape);
                    tree.Order = -22; entries.Add(tree); lastTree = x;
                }
                else if (flat && x - lastTree > 3 && x - lastStone >= options.StoneSpacing &&
                    TerrainRandom.Noise(seed + 2, x) < options.Density * .28)
                {
                    int variant = (int)(TerrainRandom.Noise(seed + 3, x) * 4) % 4;
                    var stone = Entry(0, x, row, "stone", Texture("spr_res_stone", variant), 2, 0);
                    stone.GroundOffsetPixels = ground.Offset(x, row, stone.Shape); entries.Add(stone);
                    lastStone = x;
                }
            }
            return entries.ToArray();
        }

        public static void Validate(SurfaceSceneryLayout layout, PlayableTerrain map)
        {
            if (layout.TerrainKey != SurfaceSceneryLayout.Key(map.Background))
                throw new InvalidOperationException("地形已变化；旧装饰锚点不能保存到此图。");
            byte[] shapes = map.CopyShapes();
            foreach (var p in layout.Capture())
            {
                if (!p.Enabled || SurfaceSceneryLayout.IsGroundTrim(p)) continue;
                if (!SurfaceSceneryLayout.IsOutdoor(p, map.Background))
                    throw new InvalidOperationException("装饰所在空域不是露天地表：" + p.Id);
                for (int x = p.Column - p.SupportRadius; x <= p.Column + p.SupportRadius; x++)
                    if (p.Row < 1 || map.Material(x, p.Row) == 0 || map.Material(x, p.Row - 1) != 0 ||
                        x == p.Column && shapes[p.Row * TerrainGenerationSettings.Width + x] != p.Shape)
                        throw new InvalidOperationException("装饰缺少与最终形状一致的支撑：" + p.Id);
            }
        }

        private static SurfaceSceneryPlacement Entry(byte shape, int column, int row, string kind,
            Texture2D texture, float scale, int support)
        {
            return new SurfaceSceneryPlacement
            {
                Id = kind + "-" + column, Column = column, Row = row, Shape = shape,
                Texture = texture, Pixels = new RectInt(0, 0, texture.width, texture.height),
                Scale = scale, SupportRadius = support, Flip = column % 2 == 0
            };
        }

        public static Texture2D Texture(string name, int frame)
        {
            string path = Sprites + name + "/" + name + "_" + frame + ".png";
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidOperationException("原始素材缺失：" + path);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
