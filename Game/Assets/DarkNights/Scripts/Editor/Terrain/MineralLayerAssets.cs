using System;
using System.IO;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Editor;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>在指定空目录制作内嵌矿层首版原生资源；矿图保留原始像素与来源，Unity 创建 meta，重复调用只读取已有定义，不覆盖作者资源。</summary>
    public static class MineralLayerAssets
    {
        public const string Root = "Assets/DarkNights/Res/Terrain/EmbeddedMinerals";
        public const string DefinitionPath = Root + "/MineralLayer.asset";
        public static readonly string[] Keys = { "iron", "gold", "copper", "silver", "diamond" };

        public static ARDMapDefinition Ensure()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(DefinitionPath);
            if (existing != null) { existing.LoadRuntimeCatalog(); return existing; }
            if (Directory.Exists(Root)) throw new IOException("矿层输出目录已存在而定义不完整；保留内容，不自动覆盖或删除。");
            Folder(Root);
            var database = ScriptableObject.CreateInstance<EditorAnyRuleDDatabase>();
            database.CatalogGuid = Guid.NewGuid().ToString("N");
            var clear = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            try
            {
                clear.SetPixels(new Color[64]); clear.Apply(); File.WriteAllBytes(Root + "/transparent.png", clear.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(clear); }
            AssetDatabase.ImportAsset(Root + "/transparent.png");
            var clearImporter = (TextureImporter)AssetImporter.GetAtPath(Root + "/transparent.png");
            Configure(clearImporter); clearImporter.spriteImportMode = SpriteImportMode.Single; clearImporter.SaveAndReimport();
            var fill = Visual(Root + "/TransparentFill.asset", new[] { AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/transparent.png") }, true);
            database.Terrains = new TerrainDefinition[Keys.Length]; database.RuleSets = new AnyRuleD[Keys.Length];
            for (int index = 0; index < Keys.Length; index++)
            {
                string key = Keys[index], folder = Root + "/" + key; Folder(folder);
                string source = Path.GetFullPath("../experiments/ore-dualgrid-v1/atlases/" + key + "-merged.png");
                string atlas = folder + "/" + key + ".png";
                File.Copy(source, atlas); AssetDatabase.ImportAsset(atlas);
                var importer = (TextureImporter)AssetImporter.GetAtPath(atlas);
                Configure(importer); importer.spriteImportMode = SpriteImportMode.Multiple;
                var slices = new SpriteMetaData[64];
                for (int variant = 0; variant < 4; variant++) for (int mask = 0; mask < 16; mask++)
                    slices[variant * 16 + mask] = new SpriteMetaData
                    {
                        name = key + "_m" + mask + "_v" + variant,
                        rect = new Rect(variant * 32 + mask % 4 * 8, 24 - mask / 4 * 8, 8, 8),
                        alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
                    };
#pragma warning disable CS0618
                importer.spritesheet = slices;
#pragma warning restore CS0618
                importer.SaveAndReimport();
                var sprites = AssetDatabase.LoadAllAssetsAtPath(atlas).OfType<Sprite>().ToDictionary(sprite => sprite.name);
                var terrain = ScriptableObject.CreateInstance<TerrainDefinition>();
                terrain.PersistentGuid = Guid.NewGuid().ToString("N"); terrain.Key = key; terrain.DisplayName = key;
                terrain.Solid = false; terrain.FillVisualSet = fill;
                AssetDatabase.CreateAsset(terrain, folder + "/Terrain.asset");
                var set = ScriptableObject.CreateInstance<AnyRuleD>();
                set.PersistentGuid = Guid.NewGuid().ToString("N"); set.TargetTerrain = terrain; set.Rules = new ARuleD[15];
                for (int mask = 1; mask < 16; mask++)
                {
                    var visual = Visual(folder + "/Mask" + mask.ToString("D2") + ".asset",
                        Enumerable.Range(0, 4).Select(variant => sprites[key + "_m" + mask + "_v" + variant]).ToArray(), false);
                    var rule = new ARuleD { RuleGuid = Guid.NewGuid().ToString("N"), DisplayName = "Mask " + mask,
                        Priority = Enumerable.Range(0, 4).Count(corner => (mask & (1 << corner)) != 0), AllowedTransforms = RuleTransformMask.Identity };
                    for (int corner = 0; corner < 4; corner++) rule.Corners[corner].Condition =
                        (mask & (1 << corner)) != 0 ? CornerPredicateKind.This : CornerPredicateKind.AnyKnown;
                    rule.Outputs = new[] { new RuleOutputSlot { SlotGuid = Guid.NewGuid().ToString("N"), Channel = "ore",
                        Coverage = RuleSlotCoverage.TerrainSurface, SortingDomain = VisualSortingDomain.Ground, VisualSet = visual } };
                    set.Rules[mask - 1] = rule;
                }
                AssetDatabase.CreateAsset(set, folder + "/Rules.asset");
                database.Terrains[index] = terrain; database.RuleSets[index] = set;
            }
            AssetDatabase.CreateAsset(database, Root + "/Catalog.asset"); AssetDatabase.SaveAssets();
            var definition = RuleCatalogBuild.CompileToNewAssets(database, DefinitionPath);
            definition.Width = 320; definition.Height = 192; definition.MinV = -191;
            EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
            return definition;
        }

        private static TileVisualSet Visual(string path, Sprite[] sprites, bool fill)
        {
            var visual = ScriptableObject.CreateInstance<TileVisualSet>();
            visual.PersistentGuid = Guid.NewGuid().ToString("N"); visual.TileableFill = fill;
            visual.SupportedTransforms = RuleTransformMask.Identity;
            visual.Variants = sprites.Select(sprite => new VisualVariant
            { VariantGuid = Guid.NewGuid().ToString("N"), ResourceId = Guid.NewGuid().ToString("N"), Sprite = sprite }).ToArray();
            AssetDatabase.CreateAsset(visual, path); return visual;
        }
        private static void Configure(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 8;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp; importer.isReadable = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        }
        private static void Folder(string path)
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) Folder(parent);
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
