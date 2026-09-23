using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Editor;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>只在新的洞穴风格目录制作 DualGrid 首版资源；32 像素原生图集保留四角位序，后续导入不覆盖人工资产。</summary>
    public static class CaveTerrainAssets
    {
        public const string Root = "Assets/DarkNights/Res/Terrain/CaveExploration/Style";
        public const string DefinitionPath = Root + "/Configuration/DualGridMap.asset";
        private const string Atlas = "Assets/DarkNights/Res/Art/Custom/CaveExploration/cave-dualgrid.png";
        private static readonly string[] Keys = { "loam", "slate", "basalt", "copper", "iron", "gold", "moss", "bedrock" };

        public static void Create()
        {
            string output = Root + "/Configuration";
            if (Directory.Exists(output)) throw new IOException("测试配置目录已存在；普通导入禁止覆盖人工资产。");
            SliceAtlas();
            Directory.CreateDirectory(output); AssetDatabase.ImportAsset(output);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToDictionary(s => s.name);
            var database = ScriptableObject.CreateInstance<EditorAnyRuleDDatabase>();
            database.CatalogGuid = Id("catalog");
            database.Terrains = new TerrainDefinition[8]; database.RuleSets = new AnyRuleD[8];
            for (int t = 0; t < 8; t++)
            {
                string folder = output + "/" + Keys[t]; Directory.CreateDirectory(folder); AssetDatabase.ImportAsset(folder);
                var terrain = ScriptableObject.CreateInstance<TerrainDefinition>();
                terrain.PersistentGuid = ReferenceGuid(Keys[t]); terrain.Key = Keys[t]; terrain.DisplayName = Keys[t];
                terrain.MaximumDurability = t == 7 ? int.MaxValue : new[] { 20, 40, 55, 55, 65, 75, 30 }[t];
                var rules = ScriptableObject.CreateInstance<AnyRuleD>();
                rules.PersistentGuid = Id(Keys[t] + "/rules"); rules.TargetTerrain = terrain; rules.Rules = new ARuleD[15];
                for (int mask = 1; mask < 16; mask++)
                {
                    var visual = ScriptableObject.CreateInstance<TileVisualSet>();
                    visual.PersistentGuid = Id(Keys[t] + "/visual/" + mask); visual.TileableFill = mask == 15;
                    visual.Variants = new VisualVariant[4];
                    for (int v = 0; v < 4; v++)
                    {
                        string key = Keys[t] + "_m" + mask + "_v" + v;
                        visual.Variants[v] = new VisualVariant { VariantGuid = Id(key + "/variant"), ResourceId = Id(key + "/resource"), Sprite = sprites[key] };
                    }
                    AssetDatabase.CreateAsset(visual, folder + "/Mask" + mask.ToString("D2") + ".asset");
                    if (mask == 15) terrain.FillVisualSet = visual;
                    var rule = new ARuleD { RuleGuid = Id(Keys[t] + "/rule/" + mask), DisplayName = "Mask " + mask };
                    for (int c = 0; c < 4; c++) rule.Corners[c].Condition = (mask & (1 << c)) != 0 ? CornerPredicateKind.This : CornerPredicateKind.Empty;
                    rule.Outputs = new[] { new RuleOutputSlot { SlotGuid = Id(Keys[t] + "/slot/" + mask), Channel = "cell", VisualSet = visual,
                        Coverage = RuleSlotCoverage.ExactCell, BoundaryContract = CellBoundaryContract.CanonicalMaterialEdgesV1 } };
                    rules.Rules[mask - 1] = rule;
                }
                AssetDatabase.CreateAsset(terrain, folder + "/Terrain.asset");
                AssetDatabase.CreateAsset(rules, folder + "/DualGridTile.asset");
                database.Terrains[t] = terrain; database.RuleSets[t] = rules;
            }
            AssetDatabase.CreateAsset(database, output + "/TerrainCatalog.asset");
            AssetDatabase.SaveAssets();
            var definition = RuleCatalogBuild.CompileToNewAssets(database, DefinitionPath);
            definition.Width = 320; definition.Height = 192; definition.MinV = -191;
            EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
            Debug.Log("Terrain test assets: 8 materials, 120 rules, 480 variants; " + DefinitionPath);
        }

        private static void SliceAtlas()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Atlas);
            if (importer == null) throw new IOException("请先准备测试 PNG。");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
            importer.sRGBTexture = true; importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
            var options = new TextureImporterSettings(); importer.ReadTextureSettings(options);
            options.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(options);
            var slices = new SpriteMetaData[512]; int index = 0;
            for (int t = 0; t < 8; t++) for (int v = 0; v < 4; v++) for (int m = 0; m < 16; m++)
                slices[index++] = new SpriteMetaData { name = Keys[t] + "_m" + m + "_v" + v,
                    rect = new Rect(v * 128 + m % 4 * 32, 1024 - (t * 128 + m / 4 * 32) - 32, 32, 32),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f) };
#pragma warning disable CS0618
            importer.spritesheet = slices;
#pragma warning restore CS0618
            importer.SaveAndReimport();
        }

        private static string Id(string key)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes("dark-nights-cave:" + key)), 0, 16).Replace("-", "").ToLowerInvariant();
        }
        private static string ReferenceGuid(string key)
        {
            var result = new StringBuilder();
            for (int i = 0; i < 4; i++) result.Append(Core.Logic.Terrain.TerrainRandom.Hash("dark-nights-h5:" + key + ":" + i).ToString("x8"));
            return result.ToString();
        }
    }
}
